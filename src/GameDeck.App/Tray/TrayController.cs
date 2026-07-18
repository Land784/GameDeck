using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using GameDeck.Core.Media;
using GameDeck.Core.Settings;
using H.NotifyIcon;

namespace GameDeck.App.Tray;

/// <summary>
/// Tray icon + context menu. Owns marshaling: Core events arrive on
/// threadpool threads and every UI touch goes through the dispatcher.
/// </summary>
public sealed class TrayController : IDisposable
{
    private readonly TaskbarIcon _icon;
    private readonly IMediaSessionService _media;
    private readonly SettingsService _settings;
    private readonly Action _resetOverlay;
    private readonly Dispatcher _dispatcher;
    private readonly MenuItem _sourceMenu;

    public TrayController(IMediaSessionService media, SettingsService settings, Action resetOverlay)
    {
        _media = media;
        _settings = settings;
        _resetOverlay = resetOverlay;
        _dispatcher = Dispatcher.CurrentDispatcher;

        _sourceMenu = new MenuItem { Header = "Media source" };
        _sourceMenu.SubmenuOpened += (_, _) => RebuildSourceMenu();
        // Placeholder child so WPF renders the submenu arrow before first open.
        _sourceMenu.Items.Add(new MenuItem { Header = "Automatic", IsCheckable = true, IsChecked = true });

        _icon = new TaskbarIcon
        {
            ToolTipText = "GameDeck — nothing playing",
            ContextMenu = BuildMenu(),
        };
        TrayIconFactory.ApplyGeneratedIcon(_icon);
        _icon.ForceCreate();

        _media.SnapshotChanged += OnSnapshotChanged;
    }

    private ContextMenu BuildMenu()
    {
        var menu = new ContextMenu();

        menu.Items.Add(MakeItem("Play / Pause", () => _ = _media.PlayPauseAsync()));
        menu.Items.Add(MakeItem("Next track", () => _ = _media.NextAsync()));
        menu.Items.Add(MakeItem("Previous track", () => _ = _media.PreviousAsync()));
        menu.Items.Add(new Separator());
        menu.Items.Add(_sourceMenu);
        menu.Items.Add(new Separator());

        var startup = new MenuItem
        {
            Header = "Start with Windows",
            IsCheckable = true,
            IsChecked = StartupManager.IsEnabled(),
        };
        // The Run key is the single source of truth; nothing mirrored to settings.
        startup.Click += (_, _) => StartupManager.SetEnabled(startup.IsChecked);
        menu.Items.Add(startup);

        menu.Items.Add(MakeItem("Reset overlay", () => _resetOverlay()));

        menu.Items.Add(new Separator());
        menu.Items.Add(MakeItem("Exit", () => Application.Current.Shutdown()));
        return menu;
    }

    private static MenuItem MakeItem(string header, Action onClick)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => onClick();
        return item;
    }

    private void RebuildSourceMenu()
    {
        _sourceMenu.Items.Clear();

        var preferred = _media.PreferredAppId;

        var automatic = new MenuItem
        {
            Header = "Automatic",
            IsCheckable = true,
            IsChecked = preferred is null,
        };
        automatic.Click += (_, _) => SetPreferred(null);
        _sourceMenu.Items.Add(automatic);

        foreach (var session in _media.Sessions)
        {
            var item = new MenuItem
            {
                Header = session.DisplayName,
                IsCheckable = true,
                IsChecked = session.AppId == preferred,
            };
            var appId = session.AppId;
            item.Click += (_, _) => SetPreferred(appId);
            _sourceMenu.Items.Add(item);
        }
    }

    private void SetPreferred(string? appId)
    {
        _media.PreferredAppId = appId;
        _settings.Update(s => s.PreferredAppId = appId);
    }

    private void OnSnapshotChanged(object? sender, MediaSnapshot? snapshot)
    {
        _dispatcher.BeginInvoke(() =>
        {
            _icon.ToolTipText = snapshot is null || snapshot.Title.Length == 0
                ? "GameDeck — nothing playing"
                : Truncate($"GameDeck — {snapshot.Title} · {snapshot.Artist}", 120);
        });
    }

    public void ShowBalloon(string title, string message)
    {
        _dispatcher.BeginInvoke(() => _icon.ShowNotification(title, message));
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..(max - 1)] + "…";

    public void Dispose()
    {
        _media.SnapshotChanged -= OnSnapshotChanged;
        _icon.Dispose();
    }
}
