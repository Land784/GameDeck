using System.IO;
using System.Windows;
using GameDeck.App.Hotkeys;
using GameDeck.App.Overlay;
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
    private Mutex? _instanceMutex;
    private MediaSessionService? _media;
    private HotkeyHost? _hotkeys;
    private TrayController? _tray;
    private OverlayController? _overlay;
    private SettingsService? _settings;
    private AdBridgeServer? _bridge;
    private ILoggerFactory? _loggerFactory;
    private ILogger? _log;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Local\ scopes the mutex to this logon session: one instance per
        // user, but other users on the machine can run their own.
        _instanceMutex = new Mutex(initiallyOwned: true, @"Local\GameDeck", out var createdNew);
        if (!createdNew)
        {
            // Another instance owns the tray icon; exit quietly.
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

        _hotkeys = new HotkeyHost();
        _hotkeys.HotkeyPressed += OnHotkeyPressed;
        var conflicts = _hotkeys.Register(_settings.Current.Hotkeys);
        if (conflicts.Count > 0)
            _log.LogWarning("Hotkey conflicts at startup: {Conflicts}", string.Join(", ", conflicts));

        _overlay = new OverlayController(
            _media,
            new OverlayStateMachine(TimeProvider.System, OverlayTimings.FromSettings(
                _settings.Current.AutoHideSeconds, _settings.Current.AnimationsEnabled)),
            _settings,
            loggerFactory.CreateLogger<OverlayController>());

        _tray = new TrayController(_media, _settings, () => _overlay?.Reset());

        SystemEvents.SessionSwitch += OnSessionSwitch;

        _ = FinishStartupAsync(conflicts);
    }

    private ILoggerFactory ConfigureLogging()
    {
        var logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GameDeck", "logs");
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(
                Path.Combine(logDir, "gamedeck-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7)
            .CreateLogger();

        // Log-only global handlers; the crash dialog is planned for Phase 4.
        DispatcherUnhandledException += (_, args) =>
        {
            Log.Fatal(args.Exception, "Unhandled exception on the UI thread");
            Log.CloseAndFlush();
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            Log.Fatal(args.ExceptionObject as Exception, "Unhandled exception (AppDomain)");
            Log.CloseAndFlush();
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Log.Error(args.Exception, "Unobserved task exception");
            args.SetObserved();
        };

        return new SerilogLoggerFactory(Log.Logger);
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
        _bridge?.Dispose();
        _tray?.Dispose();
        _overlay?.Dispose();
        _hotkeys?.Dispose();
        _ = _media?.DisposeAsync();
        _instanceMutex?.Dispose();
        _log?.LogInformation("GameDeck exiting");
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
