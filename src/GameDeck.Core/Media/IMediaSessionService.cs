namespace GameDeck.Core.Media;

public interface IMediaSessionService : IAsyncDisposable
{
    Task InitializeAsync();

    /// <summary>Latest snapshot, or null when nothing is playing.</summary>
    MediaSnapshot? Current { get; }

    /// <summary>
    /// Playback progress, interpolated locally between the source's coarse
    /// updates while playing. Null when nothing is playing.
    /// </summary>
    MediaTimeline? CurrentTimeline { get; }

    IReadOnlyList<SessionInfo> Sessions { get; }

    /// <summary>Pin to a specific source app; null follows the system's current session.</summary>
    string? PreferredAppId { get; set; }

    /// <summary>Raised on threadpool threads; consumers marshal to their own context.</summary>
    event EventHandler<MediaSnapshot?>? SnapshotChanged;

    /// <summary>
    /// Re-selects the session, re-attaches event handlers, and rebuilds the
    /// snapshot. Call after events that can silently invalidate WinRT
    /// subscriptions (session unlock, display changes).
    /// </summary>
    void Refresh();

    Task PlayPauseAsync();
    Task NextAsync();
    Task PreviousAsync();
}
