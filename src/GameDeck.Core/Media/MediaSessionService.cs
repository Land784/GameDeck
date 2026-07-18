namespace GameDeck.Core.Media;

/// <summary>
/// Session selection + snapshot pipeline over <see cref="ISmtcFacade"/>.
/// SMTC fires bursts of property-changed events on track change; a trailing
/// debounce coalesces them, and <see cref="MediaSnapshot"/> value equality
/// suppresses no-op notifications.
/// </summary>
public sealed class MediaSessionService : IMediaSessionService
{
    public static readonly TimeSpan DebounceInterval = TimeSpan.FromMilliseconds(150);

    private readonly ISmtcFacade _facade;
    private readonly ITimer _debounceTimer;
    private readonly object _gate = new();

    private ISmtcSession? _attached;
    private string? _preferredAppId;
    private MediaSnapshot? _current;
    private bool _disposed;

    public MediaSessionService(ISmtcFacade facade, TimeProvider? timeProvider = null)
    {
        _facade = facade;
        var tp = timeProvider ?? TimeProvider.System;
        _debounceTimer = tp.CreateTimer(
            _ => _ = RebuildSnapshotAsync(),
            state: null,
            dueTime: Timeout.InfiniteTimeSpan,
            period: Timeout.InfiniteTimeSpan);
    }

    public MediaSnapshot? Current
    {
        get { lock (_gate) return _current; }
    }

    public IReadOnlyList<SessionInfo> Sessions =>
        _facade.Sessions.Select(s => new SessionInfo(s.AppId, FriendlyName(s.AppId))).ToList();

    public string? PreferredAppId
    {
        get { lock (_gate) return _preferredAppId; }
        set
        {
            lock (_gate) _preferredAppId = value;
            ReattachAndRefresh();
        }
    }

    public event EventHandler<MediaSnapshot?>? SnapshotChanged;

    public Task InitializeAsync()
    {
        return InitCoreAsync();

        async Task InitCoreAsync()
        {
            await _facade.InitializeAsync().ConfigureAwait(false);
            _facade.CurrentSessionChanged += OnTopologyChanged;
            _facade.SessionsChanged += OnTopologyChanged;
            ReattachAndRefresh();
        }
    }

    public Task PlayPauseAsync() => RunCommandAsync(s => s.TryPlayPauseAsync());
    public Task NextAsync() => RunCommandAsync(s => s.TrySkipNextAsync());
    public Task PreviousAsync() => RunCommandAsync(s => s.TrySkipPreviousAsync());

    /// <summary>Preferred session if pinned and present, else the system current.</summary>
    private ISmtcSession? SelectSession()
    {
        string? preferred;
        lock (_gate) preferred = _preferredAppId;

        if (preferred is not null)
        {
            var pinned = _facade.Sessions.FirstOrDefault(s => s.AppId == preferred);
            if (pinned is not null) return pinned;
        }

        return _facade.CurrentSession;
    }

    private async Task RunCommandAsync(Func<ISmtcSession, Task<bool>> command)
    {
        var session = SelectSession();
        if (session is null) return;
        try
        {
            // Sources can reject commands in some states; never throw to the hotkey path.
            await command(session).ConfigureAwait(false);
        }
        catch
        {
        }
    }

    private void OnTopologyChanged(object? sender, EventArgs e) => ReattachAndRefresh();

    private void OnSessionPropertyChanged(object? sender, EventArgs e) => KickDebounce();

    private void ReattachAndRefresh()
    {
        var target = SelectSession();
        lock (_gate)
        {
            if (!ReferenceEquals(_attached, target))
            {
                Unsubscribe(_attached);
                _attached = target;
                Subscribe(target);
            }
        }

        KickDebounce();
    }

    private void Subscribe(ISmtcSession? session)
    {
        if (session is null) return;
        session.MediaPropertiesChanged += OnSessionPropertyChanged;
        session.PlaybackInfoChanged += OnSessionPropertyChanged;
        session.TimelinePropertiesChanged += OnSessionPropertyChanged;
    }

    private void Unsubscribe(ISmtcSession? session)
    {
        if (session is null) return;
        session.MediaPropertiesChanged -= OnSessionPropertyChanged;
        session.PlaybackInfoChanged -= OnSessionPropertyChanged;
        session.TimelinePropertiesChanged -= OnSessionPropertyChanged;
    }

    private void KickDebounce()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _debounceTimer.Change(DebounceInterval, Timeout.InfiniteTimeSpan);
        }
    }

    private async Task RebuildSnapshotAsync()
    {
        ISmtcSession? session;
        lock (_gate) session = _attached;

        MediaSnapshot? snapshot = null;
        if (session is not null)
        {
            try
            {
                var props = await session.TryGetMediaPropertiesAsync().ConfigureAwait(false);
                var (position, duration) = session.GetTimeline();
                snapshot = new MediaSnapshot(
                    props?.Title ?? string.Empty,
                    props?.Artist ?? string.Empty,
                    props?.AlbumTitle ?? string.Empty,
                    session.GetPlaybackState(),
                    position,
                    duration,
                    session.AppId)
                {
                    AlbumArtPng = props?.AlbumArtPng,
                };
            }
            catch
            {
                // Session vanished mid-read; treat as nothing playing.
            }
        }

        bool changed;
        lock (_gate)
        {
            changed = !Equals(_current, snapshot);
            _current = snapshot;
        }

        if (changed)
            SnapshotChanged?.Invoke(this, snapshot);
    }

    private static string FriendlyName(string appId)
    {
        // AppUserModelIds look like "Spotify.exe" or "MSEdge"; strip the extension.
        var name = appId.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? appId[..^4]
            : appId;
        return name.Length == 0 ? appId : name;
    }

    public async ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            Unsubscribe(_attached);
            _attached = null;
        }

        _facade.CurrentSessionChanged -= OnTopologyChanged;
        _facade.SessionsChanged -= OnTopologyChanged;
        _debounceTimer.Dispose();
        await _facade.DisposeAsync().ConfigureAwait(false);
    }
}
