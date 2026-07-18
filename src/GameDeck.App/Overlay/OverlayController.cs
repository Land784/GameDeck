using System.Windows.Threading;
using GameDeck.Core.Bridge;
using GameDeck.Core.Media;
using GameDeck.Core.Overlay;
using GameDeck.Core.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace GameDeck.App.Overlay;

/// <summary>
/// Wires media events and hotkeys into the state machine, and maps machine
/// states onto the window (show/hide, fades, timers). Owns marshaling to
/// the UI thread. Create on the UI thread.
/// </summary>
public sealed class OverlayController : IDisposable
{
    private static readonly TimeSpan TopmostGuardInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan ProgressInterval = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan InteractiveRevertAfter = TimeSpan.FromSeconds(30);

    private readonly IMediaSessionService _media;
    private readonly OverlayStateMachine _machine;
    private readonly SettingsService _settings;
    private OverlayTimings _timings;
    private readonly ILogger _logger;
    private readonly Dispatcher _dispatcher;
    private readonly OverlayViewModel _vm = new();
    private readonly OverlayWindow _window;
    private readonly DispatcherTimer _topmostGuard;
    private readonly DispatcherTimer _progressTimer;
    private readonly DispatcherTimer _interactiveRevert;

    public OverlayController(
        IMediaSessionService media,
        OverlayStateMachine machine,
        SettingsService settings,
        ILogger logger)
    {
        _media = media;
        _machine = machine;
        _settings = settings;
        _timings = OverlayTimings.FromSettings(
            settings.Current.AutoHideSeconds, settings.Current.AnimationsEnabled);
        _logger = logger;
        _dispatcher = Dispatcher.CurrentDispatcher;

        _window = new OverlayWindow { DataContext = _vm };

        _topmostGuard = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TopmostGuardInterval,
        };
        _topmostGuard.Tick += (_, _) => _window.AssertTopmost();

        _progressTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = ProgressInterval,
        };
        _progressTimer.Tick += (_, _) => UpdateProgress();

        // Failsafe: an invisible click-eating window is this app's worst bug.
        _interactiveRevert = new DispatcherTimer { Interval = InteractiveRevertAfter };
        _interactiveRevert.Tick += (_, _) =>
        {
            _logger.LogInformation("Interactive mode auto-reverted after inactivity");
            SetInteractive(false);
        };

        _machine.StateChanged += OnStateChanged;
        _media.SnapshotChanged += OnSnapshotChanged;
        _window.MouseEnter += (_, _) => _machine.PointerEntered();
        _window.MouseLeave += (_, _) => _machine.PointerExited();
        _window.MouseMove += (_, _) => ResetInteractiveRevert();
        _window.DragCompleted += PersistPlacement;
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    private double TargetOpacity => Math.Clamp(_settings.Current.OverlayOpacity, 0.3, 1.0);

    /// <summary>Positions the window from persisted placement and shows it.
    /// A missing monitor falls back to the primary.</summary>
    private void ShowPlaced()
    {
        var s = _settings.Current;
        var monitors = MonitorInterop.GetMonitors();
        var monitor = MonitorInterop.ByNameOrPrimary(monitors, s.OverlayMonitor);
        var height = _window.ActualHeight > 0 ? _window.ActualHeight : 96;
        var (left, top) = OverlayPlacement.Position(
            monitor.WorkAreaDip, _window.Width, height, s.OverlayCorner, s.OverlayOffsetX, s.OverlayOffsetY);
        _window.ShowAt(left, top);
    }

    /// <summary>Drag ended: snap to a corner placement, persist, reposition.</summary>
    private void PersistPlacement()
    {
        var monitors = MonitorInterop.GetMonitors();
        var window = new RectD(_window.Left, _window.Top, _window.ActualWidth, _window.ActualHeight);
        var monitor = MonitorInterop.ByPointOrPrimary(monitors, window.CenterX, window.CenterY);
        var (corner, offsetX, offsetY) = OverlayPlacement.FromWindow(monitor.WorkAreaDip, window);

        _settings.Update(s =>
        {
            s.OverlayMonitor = monitor.DeviceName;
            s.OverlayCorner = corner;
            s.OverlayOffsetX = offsetX;
            s.OverlayOffsetY = offsetY;
        });
        ShowPlaced();
        _logger.LogInformation("Overlay placement saved: {Monitor} {Corner} +{X},+{Y}",
            monitor.DeviceName, corner, offsetX, offsetY);
    }

    /// <summary>Settings window applied changes: re-derive timings, opacity, position.</summary>
    public void ApplySettingsChanged()
    {
        _timings = OverlayTimings.FromSettings(
            _settings.Current.AutoHideSeconds, _settings.Current.AnimationsEnabled);
        _machine.UpdateTimings(_timings);
        if (_machine.State is OverlayState.Visible or OverlayState.FadingIn)
        {
            ShowPlaced();
            _window.BeginFade(TargetOpacity, TimeSpan.Zero);
        }
    }

    public void ToggleVisibility() => _machine.ToggleVisibility();

    /// <summary>Bridge callback; arrives on a threadpool thread.</summary>
    public void OnAdStateChanged(AdStatus? status)
    {
        _dispatcher.BeginInvoke(() =>
        {
            _vm.SetAdState(status);
            _machine.NotifyAdStateChanged(status is not null);
        });
    }

    public void ToggleInteractivity() => SetInteractive(!_machine.IsInteractive);

    /// <summary>Tray failsafe: force click-through and default position.</summary>
    public void Reset()
    {
        SetInteractive(false);
        _settings.Update(s =>
        {
            s.OverlayMonitor = null;
            s.OverlayCorner = OverlayCorner.TopRight;
            s.OverlayOffsetX = OverlayPlacement.DefaultMargin;
            s.OverlayOffsetY = OverlayPlacement.DefaultMargin;
        });
        if (_machine.State is OverlayState.Visible or OverlayState.FadingIn)
            ShowPlaced();
    }

    private void SetInteractive(bool interactive)
    {
        _machine.SetInteractive(interactive);
        _vm.IsInteractive = interactive;
        _window.SetClickThrough(!interactive);
        _interactiveRevert.IsEnabled = interactive;
        _logger.LogDebug("Overlay interactivity: {Interactive}", interactive);
    }

    private void ResetInteractiveRevert()
    {
        if (!_interactiveRevert.IsEnabled) return;
        _interactiveRevert.Stop();
        _interactiveRevert.Start();
    }

    private void OnSnapshotChanged(object? sender, MediaSnapshot? snapshot)
    {
        _dispatcher.BeginInvoke(() =>
        {
            _vm.UpdateFromSnapshot(snapshot);
            if (snapshot is not null)
                _machine.NotifyTrackChanged();
        });
    }

    private void OnStateChanged(object? sender, OverlayState state)
    {
        _dispatcher.BeginInvoke(() => ApplyState(state));
    }

    private void ApplyState(OverlayState state)
    {
        _logger.LogDebug("Overlay state: {State}", state);
        switch (state)
        {
            case OverlayState.FadingIn:
                UpdateProgress();
                ShowPlaced();
                _window.BeginFade(TargetOpacity, _timings.FadeIn);
                _topmostGuard.Start();
                _progressTimer.Start();
                break;
            case OverlayState.Visible:
                break; // Fade animation has already landed at 1.0.
            case OverlayState.FadingOut:
                _window.BeginFade(0.0, _timings.FadeOut);
                break;
            case OverlayState.Hidden:
                _topmostGuard.Stop();
                _progressTimer.Stop();
                _window.Hide();
                break;
        }
    }

    private void UpdateProgress()
    {
        var timeline = _media.CurrentTimeline;
        _vm.Progress = timeline is { Duration.TotalSeconds: > 0 }
            ? Math.Clamp(timeline.Position / timeline.Duration, 0, 1)
            : 0;
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        _dispatcher.BeginInvoke(() =>
        {
            if (_machine.State is OverlayState.Visible or OverlayState.FadingIn)
            {
                ShowPlaced();
                _logger.LogInformation("Display settings changed; overlay repositioned");
            }
        });
    }

    public void Dispose()
    {
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        _media.SnapshotChanged -= OnSnapshotChanged;
        _machine.StateChanged -= OnStateChanged;
        _topmostGuard.Stop();
        _progressTimer.Stop();
        _interactiveRevert.Stop();
        _machine.Dispose();
        _window.Close();
    }
}
