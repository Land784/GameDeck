namespace GameDeck.Core.Media;

public interface IMediaSessionService : IAsyncDisposable
{
    Task InitializeAsync();

    /// <summary>Latest snapshot, or null when nothing is playing.</summary>
    MediaSnapshot? Current { get; }

    IReadOnlyList<SessionInfo> Sessions { get; }

    /// <summary>Pin to a specific source app; null follows the system's current session.</summary>
    string? PreferredAppId { get; set; }

    /// <summary>Raised on threadpool threads; consumers marshal to their own context.</summary>
    event EventHandler<MediaSnapshot?>? SnapshotChanged;

    Task PlayPauseAsync();
    Task NextAsync();
    Task PreviousAsync();
}
