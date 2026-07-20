using System.IO;
using System.Windows;
using GameDeck.App.Diagnostics;
using GameDeck.App.Hotkeys;
using GameDeck.App.Overlay;
using GameDeck.App.Settings;
using GameDeck.App.Tray;
using GameDeck.Core.Bridge;
using GameDeck.Core.Hotkeys;
using GameDeck.Core.Media;
using GameDeck.Core.Overlay;
using GameDeck.Core.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Serilog;
using Serilog.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace GameDeck.App;

public partial class App : Application
{
    private const string ActivateEventName = @"Local\GameDeck.Activate";

    private Mutex? _instanceMutex;
    private EventWaitHandle? _activateEvent;
    private RegisteredWaitHandle? _activateWait;
    private MediaSessionService? _media;
    private HotkeyHost? _hotkeys;
    private HotkeyFallbackMatcher? _fallbackMatcher;
    private KeyboardHookHost? _keyboardHook;
    private TrayController? _tray;
    private OverlayController? _overlay;
    private SettingsService? _settings;
    private AdBridgeServer? _bridge;
    private ILoggerFactory? _loggerFactory;
    private ILogger? _log;
    private string _logDir = "";
    private int _crashDialogShown;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Local\ scopes the mutex to this logon session: one instance per
        // user, but other users on the machine can run their own.
        _instanceMutex = new Mutex(initiallyOwned: true, @"Local\GameDeck", out var createdNew);
        if (!createdNew)
        {
            // Another instance owns the tray icon; poke it so it can tell the
            // user where it lives, then exit quietly.
            try
            {
                using var activate = EventWaitHandle.OpenExisting(ActivateEventName);
                activate.Set();
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                // First instance predates this build or is mid-shutdown; nothing to poke.
            }
            Shutdown();
            return;
        }

        var loggerFactory = ConfigureLogging();
        _loggerFactory = loggerFactory;
        _log = loggerFactory.CreateLogger("App");
        _log.LogInformation("GameDeck {Version} starting",
            typeof(App).Assembly.GetName().Version?.ToString(3));

        _settings = new SettingsService(logger: loggerFactory.CreateLogger<SettingsService>());
        _settings.Load();

        _media = new MediaSessionService(
            new SmtcFacade(loggerFactory.CreateLogger<SmtcFacade>()),
            logger: loggerFactory.CreateLogger<MediaSessionService>())
        {
            PreferredAppId = _settings.Current.PreferredAppId,
        };

        _fallbackMatcher = new HotkeyFallbackMatcher();
        _hotkeys = new HotkeyHost();
        _hotkeys.HotkeyPressed += OnRegisteredHotkeyPressed;
        _hotkeys.BindingsRegistered += _fallbackMatcher.SetBindings;
        var conflicts = _hotkeys.Register(_settings.Current.Hotkeys);
        if (conflicts.Count > 0)
            _log.LogWarning("Hotkey conflicts at startup: {Conflicts}", string.Join(", ", conflicts));
        _keyboardHook = new KeyboardHookHost(
            _fallbackMatcher, OnHotkeyPressed, loggerFactory.CreateLogger<KeyboardHookHost>());

        _overlay = new OverlayController(
            _media,
            new OverlayStateMachine(TimeProvider.System, OverlayTimings.FromSettings(
                _settings.Current.AutoHideSeconds, _settings.Current.AnimationsEnabled)),
            _settings,
            loggerFactory.CreateLogger<OverlayController>());

        _tray = new TrayController(_media, _settings, () => _overlay?.Reset(), OpenSettings);

        _activateEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ActivateEventName);
        _activateWait = ThreadPool.RegisterWaitForSingleObject(
            _activateEvent,
            (_, _) => _tray?.ShowBalloon(
                "GameDeck is already running",
                "Look for the note icon in the system tray."),
            null, Timeout.Infinite, executeOnlyOnce: false);

        SystemEvents.SessionSwitch += OnSessionSwitch;

        _ = FinishStartupAsync(conflicts);
    }

    private ILoggerFactory ConfigureLogging()
    {
        _logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GameDeck", "logs");
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(
                Path.Combine(_logDir, "gamedeck-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7)
            .CreateLogger();

        // Fatal handlers: log, tell the user, then exit. UnobservedTaskException
        // stays log-only (background faults should not tear the app down).
        DispatcherUnhandledException += (_, args) =>
        {
            Log.Fatal(args.Exception, "Unhandled exception on the UI thread");
            Log.CloseAndFlush();
            ShowCrashDialogOnce();
            // We have already logged and shown the dialog; suppress WPF's own
            // crash so the app exits cleanly instead of via a Windows error.
            args.Handled = true;
            Shutdown();
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            Log.Fatal(args.ExceptionObject as Exception, "Unhandled exception (AppDomain)");
            Log.CloseAndFlush();
            ShowCrashDialogOnce();
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Log.Error(args.Exception, "Unobserved task exception");
            args.SetObserved();
        };

        return new SerilogLoggerFactory(Log.Logger);
    }

    // Both fatal handlers can fire for one failure; show the dialog at most once.
    // The hook may run off the UI thread (AppDomain), so marshal to the dispatcher.
    private void ShowCrashDialogOnce()
    {
        if (Interlocked.Exchange(ref _crashDialogShown, 1) != 0) return;

        if (Dispatcher.CheckAccess())
            CrashDialog.Show(_logDir);
        else
            Dispatcher.Invoke(() => CrashDialog.Show(_logDir));
    }

    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        if (e.Reason != SessionSwitchReason.SessionUnlock) return;

        // SystemEvents raises on its own broadcast thread; RegisterHotKey must
        // run on the thread that owns the message window.
        Dispatcher.BeginInvoke(() =>
        {
            _log?.LogInformation("Session unlocked; re-registering hotkeys and refreshing media");
            var conflicts = _hotkeys?.ReRegister() ?? Array.Empty<HotkeyAction>();
            if (conflicts.Count > 0)
                _log?.LogWarning("Hotkey conflicts after unlock: {Conflicts}", string.Join(", ", conflicts));
            _keyboardHook?.Reinstall();
            _media?.Refresh();
        });
    }

    private async Task FinishStartupAsync(IReadOnlyList<HotkeyAction> conflicts)
    {
        // The bridge is independent of media; a Spotify failure must not kill ad-skip.
        StartBridge();

        try
        {
            await _media!.InitializeAsync();
            _log?.LogInformation("Media engine initialized");
        }
        catch (Exception ex)
        {
            _log?.LogError(ex, "Media engine failed to initialize");
            _tray?.ShowBalloon("GameDeck", $"Media integration failed to start: {ex.Message}");
            return;
        }

        if (conflicts.Count > 0)
        {
            _tray?.ShowBalloon(
                "GameDeck hotkey conflict",
                $"Already taken by another app: {string.Join(", ", conflicts)}.");
        }

        MaybeShowFirstRunTour();
    }

    // First launch only: one balloon pointing at the overlay hotkey and tray
    // menu, plus a brief overlay pop. No wizard. The flag makes it fire once.
    private void MaybeShowFirstRunTour()
    {
        if (_settings is null || _settings.Current.FirstRunShown) return;

        _tray?.ShowBalloon(
            "GameDeck is running",
            "Ctrl+Alt+O shows the overlay. Right-click the note icon for settings.");
        _overlay?.ShowWelcome();
        _settings.Update(s => s.FirstRunShown = true);
    }

    private SettingsWindow? _settingsWindow;

    private void OpenSettings()
    {
        if (_settingsWindow is not null)
        {
            _settingsWindow.Activate();
            return;
        }
        if (_settings is null || _hotkeys is null || _media is null || _overlay is null)
            return;

        _settingsWindow = new SettingsWindow(_settings, _hotkeys, _media, _overlay, () => _bridge);
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
    }

    private void StartBridge()
    {
        var token = _settings?.Current.BridgeToken;
        if (token is null)
        {
            _log?.LogWarning("No bridge token in settings; bridge not started");
            return;
        }

        var tracker = new AdStateTracker();
        tracker.ActiveAdChanged += (_, status) => _overlay?.OnAdStateChanged(status);
        _bridge = new AdBridgeServer(token, tracker, TimeProvider.System,
            _loggerFactory?.CreateLogger<AdBridgeServer>());
        _bridge.Start();
    }

    // WM_HOTKEY path: suppress if the low-level hook already fired it.
    private void OnRegisteredHotkeyPressed(HotkeyAction action)
    {
        if (_fallbackMatcher?.OnRegisteredHotkey(action) ?? true)
            OnHotkeyPressed(action);
    }

    private void OnHotkeyPressed(HotkeyAction action)
    {
        if (_media is null) return;

        switch (action)
        {
            case HotkeyAction.PlayPause:
                _ = _media.PlayPauseAsync();
                break;
            case HotkeyAction.NextTrack:
                _ = _media.NextAsync();
                break;
            case HotkeyAction.PreviousTrack:
                _ = _media.PreviousAsync();
                break;
            case HotkeyAction.ToggleOverlay:
                _overlay?.ToggleVisibility();
                break;
            case HotkeyAction.ToggleInteractivity:
                _overlay?.ToggleInteractivity();
                break;
            case HotkeyAction.SkipAd:
                _ = _bridge?.SendSkipAsync();
                break;
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        _activateWait?.Unregister(null);
        _activateEvent?.Dispose();
        _bridge?.Dispose();
        _tray?.Dispose();
        _overlay?.Dispose();
        _keyboardHook?.Dispose();
        _hotkeys?.Dispose();
        _ = _media?.DisposeAsync();
        _instanceMutex?.Dispose();
        _log?.LogInformation("GameDeck exiting");
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
