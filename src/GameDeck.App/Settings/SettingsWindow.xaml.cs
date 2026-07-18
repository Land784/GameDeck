using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using GameDeck.App.Hotkeys;
using GameDeck.App.Overlay;
using GameDeck.Core.Bridge;
using GameDeck.Core.Hotkeys;
using GameDeck.Core.Media;
using GameDeck.Core.Overlay;
using GameDeck.Core.Settings;

namespace GameDeck.App.Settings;

/// <summary>
/// The settings dialog. Thin and imperative: every change persists
/// immediately through SettingsService.Update and pokes the affected
/// component; there is no OK/Cancel model to get out of sync.
/// </summary>
public partial class SettingsWindow : Window
{
    private static readonly Dictionary<HotkeyAction, string> ActionNames = new()
    {
        [HotkeyAction.PlayPause] = "Play / pause",
        [HotkeyAction.NextTrack] = "Next track",
        [HotkeyAction.PreviousTrack] = "Previous track",
        [HotkeyAction.ToggleOverlay] = "Show / hide overlay",
        [HotkeyAction.ToggleInteractivity] = "Overlay interactivity",
        [HotkeyAction.SkipAd] = "Skip YouTube ad",
    };

    private readonly SettingsService _settings;
    private readonly HotkeyHost _hotkeys;
    private readonly IMediaSessionService _media;
    private readonly OverlayController _overlay;
    private readonly Func<AdBridgeServer?> _bridge;

    private HotkeyAction? _recording;
    private bool _loading = true;

    public SettingsWindow(
        SettingsService settings,
        HotkeyHost hotkeys,
        IMediaSessionService media,
        OverlayController overlay,
        Func<AdBridgeServer?> bridge)
    {
        _settings = settings;
        _hotkeys = hotkeys;
        _media = media;
        _overlay = overlay;
        _bridge = bridge;

        InitializeComponent();
        LoadGeneral();
        LoadOverlay();
        RebuildHotkeyRows();
        LoadMedia();
        LoadBridge();
        LoadAbout();
        _loading = false;

        PreviewKeyDown += OnRecorderKeyDown;
    }

    private void LoadGeneral()
    {
        StartWithWindowsBox.IsChecked = StartupManager.IsEnabled();
        StartWithWindowsBox.Click += (_, _) => StartupManager.SetEnabled(StartWithWindowsBox.IsChecked == true);

        AnimationsBox.IsChecked = _settings.Current.AnimationsEnabled;
        AnimationsBox.Click += (_, _) =>
        {
            _settings.Update(s => s.AnimationsEnabled = AnimationsBox.IsChecked == true);
            _overlay.ApplySettingsChanged();
        };
    }

    private void LoadOverlay()
    {
        OpacitySlider.Value = Math.Clamp(_settings.Current.OverlayOpacity, 0.3, 1.0);
        OpacityLabel.Text = $"{OpacitySlider.Value:P0}";
        OpacitySlider.ValueChanged += (_, _) =>
        {
            OpacityLabel.Text = $"{OpacitySlider.Value:P0}";
            if (_loading) return;
            _settings.Update(s => s.OverlayOpacity = OpacitySlider.Value);
            _overlay.ApplySettingsChanged();
            _overlay.Preview(); // Live preview: show the card at the new opacity.
        };

        AutoHideBox.Items.Add("Always visible");
        for (var s = 2; s <= 15; s++)
            AutoHideBox.Items.Add($"{s} seconds");
        var current = _settings.Current.AutoHideSeconds;
        AutoHideBox.SelectedIndex = current <= 0 ? 0 : Math.Clamp(current, 2, 15) - 1;
        AutoHideBox.SelectionChanged += (_, _) =>
        {
            if (_loading) return;
            var seconds = AutoHideBox.SelectedIndex == 0 ? 0 : AutoHideBox.SelectedIndex + 1;
            _settings.Update(s => s.AutoHideSeconds = seconds);
            _overlay.ApplySettingsChanged();
        };

        WireCorner(CornerTL, OverlayCorner.TopLeft);
        WireCorner(CornerTR, OverlayCorner.TopRight);
        WireCorner(CornerBL, OverlayCorner.BottomLeft);
        WireCorner(CornerBR, OverlayCorner.BottomRight);
    }

    private void WireCorner(Button button, OverlayCorner corner)
    {
        button.Click += (_, _) =>
        {
            _settings.Update(s =>
            {
                s.OverlayCorner = corner;
                s.OverlayOffsetX = OverlayPlacement.DefaultMargin;
                s.OverlayOffsetY = OverlayPlacement.DefaultMargin;
            });
            _overlay.ApplySettingsChanged();
            _overlay.Preview();
        };
    }

    private void RebuildHotkeyRows()
    {
        HotkeyList.Items.Clear();
        foreach (var binding in _settings.Current.Hotkeys)
        {
            var row = new DockPanel { Margin = new Thickness(0, 3, 0, 3) };

            var record = new Button
            {
                Content = _recording == binding.Action ? "Press keys…" : "Record",
                Width = 90,
                Tag = binding.Action,
            };
            record.Click += (_, _) =>
            {
                _recording = (HotkeyAction)record.Tag;
                RebuildHotkeyRows();
            };
            DockPanel.SetDock(record, Dock.Right);
            row.Children.Add(record);

            var badge = new TextBlock
            {
                Text = _hotkeys.Conflicts.Contains(binding.Action) ? "in use by another app" : string.Empty,
                Foreground = Brushes.IndianRed,
                Width = 130,
                VerticalAlignment = VerticalAlignment.Center,
            };
            DockPanel.SetDock(badge, Dock.Right);
            row.Children.Add(badge);

            var combo = new TextBlock
            {
                Text = Describe(binding),
                Width = 120,
                VerticalAlignment = VerticalAlignment.Center,
            };
            DockPanel.SetDock(combo, Dock.Right);
            row.Children.Add(combo);

            row.Children.Add(new TextBlock
            {
                Text = ActionNames.GetValueOrDefault(binding.Action, binding.Action.ToString()),
                VerticalAlignment = VerticalAlignment.Center,
            });

            HotkeyList.Items.Add(row);
        }
    }

    private void OnRecorderKeyDown(object sender, KeyEventArgs e)
    {
        if (_recording is not { } action)
            return;
        e.Handled = true;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Escape)
        {
            _recording = null;
            RebuildHotkeyRows();
            return;
        }
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
        {
            return; // Modifiers alone are not a combo; keep listening.
        }

        var modifiers = HotkeyModifiers.None;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) modifiers |= HotkeyModifiers.Control;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) modifiers |= HotkeyModifiers.Alt;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) modifiers |= HotkeyModifiers.Shift;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Windows)) modifiers |= HotkeyModifiers.Win;
        if (modifiers == HotkeyModifiers.None)
            return; // A bare key would swallow normal typing globally.

        var newBindings = _settings.Current.Hotkeys
            .Select(b => b.Action == action
                ? b with { Modifiers = modifiers, VirtualKey = (uint)KeyInterop.VirtualKeyFromKey(key) }
                : b)
            .ToList();

        // Register first so Conflicts reflects the new set, then persist.
        _hotkeys.Register(newBindings);
        _settings.Update(s => s.Hotkeys = newBindings);
        _recording = null;
        RebuildHotkeyRows();
    }

    private static string Describe(HotkeyBinding binding)
    {
        var parts = new List<string>(4);
        if (binding.Modifiers.HasFlag(HotkeyModifiers.Control)) parts.Add("Ctrl");
        if (binding.Modifiers.HasFlag(HotkeyModifiers.Alt)) parts.Add("Alt");
        if (binding.Modifiers.HasFlag(HotkeyModifiers.Shift)) parts.Add("Shift");
        if (binding.Modifiers.HasFlag(HotkeyModifiers.Win)) parts.Add("Win");
        parts.Add(KeyInterop.KeyFromVirtualKey((int)binding.VirtualKey).ToString());
        return string.Join("+", parts);
    }

    private void LoadMedia()
    {
        FillSources();
        SourceBox.DropDownOpened += (_, _) => FillSources();
        SourceBox.SelectionChanged += (_, _) =>
        {
            if (_loading || SourceBox.SelectedItem is not ComboBoxItem item) return;
            var appId = item.Tag as string;
            _media.PreferredAppId = appId;
            _settings.Update(s => s.PreferredAppId = appId);
        };
    }

    private void FillSources()
    {
        var preferred = _media.PreferredAppId;
        SourceBox.Items.Clear();
        SourceBox.Items.Add(new ComboBoxItem { Content = "Automatic", Tag = null });
        foreach (var session in _media.Sessions)
            SourceBox.Items.Add(new ComboBoxItem { Content = session.DisplayName, Tag = session.AppId });

        SourceBox.SelectedIndex = 0;
        for (var i = 1; i < SourceBox.Items.Count; i++)
        {
            if (((ComboBoxItem)SourceBox.Items[i]).Tag as string == preferred)
                SourceBox.SelectedIndex = i;
        }
    }

    private void LoadBridge()
    {
        TokenBox.Text = _settings.Current.BridgeToken ?? string.Empty;
        CopyTokenButton.Click += (_, _) =>
        {
            if (TokenBox.Text.Length > 0) Clipboard.SetText(TokenBox.Text);
        };

        RefreshBridgeStatus();
        if (_bridge() is { } server)
        {
            server.ExtensionConnectedChanged += OnBridgeChanged;
            Closed += (_, _) => server.ExtensionConnectedChanged -= OnBridgeChanged;
        }
    }

    private void OnBridgeChanged(object? sender, bool connected) =>
        Dispatcher.BeginInvoke(RefreshBridgeStatus);

    private void RefreshBridgeStatus()
    {
        var server = _bridge();
        BridgeStatusText.Text = server switch
        {
            null or { Port: null } => "Bridge not running",
            { ExtensionConnected: true } => "Extension connected",
            _ => "Waiting for the extension…",
        };
        BridgePortText.Text = server?.Port is { } port ? $"Listening on 127.0.0.1:{port}" : string.Empty;
    }

    private void LoadAbout()
    {
        VersionText.Text = $"GameDeck {typeof(SettingsWindow).Assembly.GetName().Version?.ToString(3)}";
        GithubLink.Click += (_, _) => Process.Start(new ProcessStartInfo
        {
            FileName = "https://github.com/Land784/GameDeck",
            UseShellExecute = true,
        });
    }
}
