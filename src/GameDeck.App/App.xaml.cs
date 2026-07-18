using System.Windows;
using GameDeck.App.Hotkeys;
using GameDeck.App.Tray;
using GameDeck.Core.Hotkeys;
using GameDeck.Core.Media;
using GameDeck.Core.Settings;

namespace GameDeck.App;

public partial class App : Application
{
    private Mutex? _instanceMutex;
    private MediaSessionService? _media;
    private HotkeyHost? _hotkeys;
    private TrayController? _tray;
    private readonly SettingsService _settings = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _instanceMutex = new Mutex(initiallyOwned: true, @"Global\GameDeck", out var createdNew);
        if (!createdNew)
        {
            // Another instance owns the tray icon; exit quietly.
            Shutdown();
            return;
        }

        _settings.Load();

        _media = new MediaSessionService(new SmtcFacade())
        {
            PreferredAppId = _settings.Current.PreferredAppId,
        };

        _hotkeys = new HotkeyHost();
        _hotkeys.HotkeyPressed += OnHotkeyPressed;
        var conflicts = _hotkeys.Register(_settings.Current.Hotkeys);

        _tray = new TrayController(_media, _settings);

        _ = FinishStartupAsync(conflicts);
    }

    private async Task FinishStartupAsync(IReadOnlyList<HotkeyAction> conflicts)
    {
        try
        {
            await _media!.InitializeAsync();
        }
        catch (Exception ex)
        {
            _tray?.ShowBalloon("GameDeck", $"Media integration failed to start: {ex.Message}");
            return;
        }

        if (conflicts.Count > 0)
        {
            _tray?.ShowBalloon(
                "GameDeck — hotkey conflict",
                $"Already taken by another app: {string.Join(", ", conflicts)}.");
        }
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
            // ToggleOverlay / ToggleInteractivity land in Phase 2, SkipAd in Phase 3.
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _hotkeys?.Dispose();
        _ = _media?.DisposeAsync();
        _instanceMutex?.Dispose();
        base.OnExit(e);
    }
}
