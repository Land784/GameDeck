using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace GameDeck.Core.Media;

/// <summary>
/// Session selection + snapshot pipeline over <see cref="ISmtcFacade"/>.
/// SMTC fires bursts of property-changed events on track change; a trailing
/// debounce coalesces them, and <see cref="MediaSnapshot"/> value equality
/// suppresses no-op notifications. Timeline ticks bypass the snapshot path
/// entirely and only update <see cref="CurrentTimeline"/>.
/// </summary>
public sealed class MediaSessionService : IMediaSessionService
{
    public static readonly TimeSpan DebounceInterval = TimeSpan.FromMilliseconds(150);

    private readonly ISmtcFacade _facade;
    private readonly TimeProvider _time;
    private readonly ILogger _logger;
    private readonly ITimer _debounceTimer;
    private readonly object _gate = new();

    private ISmtcSession? _attached;
    private string? _preferredAppId;
    private MediaSnapshot? _current;
    private MediaTimeline? _timeline;
    private long _timelineTimestamp;
    private bool _disposed;

    public MediaSessionService(ISmtcFacade facade, TimeProvider? timeProvider = null, ILogger? logger = null)
    {
        _facade = facade;
        _time = timeProvider ?? TimeProvider.System;
        _logger = logger ?? NullLogger.Instance;
        _debounceTimer = _time.CreateTimer(
            _ => _ = RebuildSnapshotAsync(),
            state: null,
            dueTime: Timeout.InfiniteTimeSpan,
            period: Timeout.InfiniteTimeSpan);
    }

    public MediaSnapshot? Current
    {
        get { lock (_gate) return _current; }
    }

    public MediaTimeline? CurrentTimeline
    {
        get
        {
            lock (_gate)
            {
                if (_timeline is null) return null;
                if (_current?.Playback != PlaybackState.Playing) return _timeline;

                var position = _timeline.Position + _time.GetElapsedTime(_timelineTimestamp);
                if (_timeline.Duration > TimeSpan.Zero && position > _timeline.Duration)
                    position = _timeline.Duration;
                return _timeline with { Position = position };
            }
        }
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

    public void Refresh() => ReattachAndRefresh();

    public Task PlayPauseAsync() => RunCommandAsync(s => s.TryPlayPauseAsync(), "play/pause");
    public Task NextAsync() => RunCommandAsync(s => s.TrySkipNextAsync(), "next");
    public Task PreviousAsync() => RunCommandAsync(s => s.TrySkipPreviousAsync(), "previous");

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

    private async Task RunCommandAsync(Func<ISmtcSession, Task<bool>> command, string name)
    {
        var session = SelectSession();
        if (session is null)
        {
            _logger.LogDebug("Media command {Command} ignored: no session", name);
            return;
        }

        try
        {
            // Sources can reject commands in some states; never throw to the hotkey path.
            var accepted = await command(session).ConfigureAwait(false);
            if (!accepted)
                _logger.LogInformation("Media command {Command} rejected by {AppId}", name, session.AppId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Media command {Command} threw for {AppId}", name, session.AppId);
        }
    }

    private void OnTopologyChanged(object? sender, EventArgs e) => ReattachAndRefresh();

    private void OnSessionPropertyChanged(object? sender, EventArgs e) => KickDebounce();

    private void OnTimelineChanged(object? sender, EventArgs e)
    {
        ISmtcSession? session;
        lock (_gate) session = _attached;
        if (session is null) return;

        try
        {
            var (position, duration) = session.GetTimeline();
            lock (_gate)
            {
                _timeline = new MediaTimeline(position, duration);
                _timelineTimestamp = _time.GetTimestamp();
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Timeline read failed for {AppId}", session.AppId);
        }
    }

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
                _logger.LogDebug("Attached to media session {AppId}", target?.AppId ?? "<none>");
            }
        }

        KickDebounce();
    }

    private void Subscribe(ISmtcSession? session)
    {
        if (session is null) return;
        session.MediaPropertiesChanged += OnSessionPropertyChanged;
        session.PlaybackInfoChanged += OnSessionPropertyChanged;
        session.TimelinePropertiesChanged += OnTimelineChanged;
    }

    private void Unsubscribe(ISmtcSession? session)
    {
        if (session is null) return;
        session.MediaPropertiesChanged -= OnSessionPropertyChanged;
        session.PlaybackInfoChanged -= OnSessionPropertyChanged;
        session.TimelinePropertiesChanged -= OnTimelineChanged;
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
        MediaTimeline? timeline = null;
        if (session is not null)
        {
            try
            {
                var props = await session.TryGetMediaPropertiesAsync().ConfigureAwait(false);
                var (position, duration) = session.GetTimeline();
                timeline = new MediaTimeline(position, duration);
                snapshot = new MediaSnapshot(
                    props?.Title ?? string.Empty,
                    props?.Artist ?? string.Empty,
                    props?.AlbumTitle ?? string.Empty,
                    session.GetPlaybackState(),
                    session.AppId)
                {
                    AlbumArtPng = props?.AlbumArtPng,
                };
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Snapshot rebuild failed; treating as nothing playing");
            }
        }

        bool changed;
        lock (_gate)
        {
            changed = !Equals(_current, snapshot);
            _current = snapshot;
            _timeline = timeline;
            _timelineTimestamp = _time.GetTimestamp();
        }

        if (changed)
        {
            _logger.LogDebug("Snapshot changed: {Title} by {Artist} [{State}]",
                snapshot?.Title, snapshot?.Artist, snapshot?.Playback);
            SnapshotChanged?.Invoke(this, snapshot);
        }
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
