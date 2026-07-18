namespace GameDeck.Core.Media;

/// <summary>
/// Thin seam over the WinRT SMTC API so <see cref="MediaSessionService"/> is
/// unit-testable. The real implementation is <see cref="SmtcFacade"/>; tests
/// substitute fakes. Events may arrive on arbitrary threadpool threads.
/// </summary>
public interface ISmtcFacade : IAsyncDisposable
{
    Task InitializeAsync();

    ISmtcSession? CurrentSession { get; }
    IReadOnlyList<ISmtcSession> Sessions { get; }

    event EventHandler? CurrentSessionChanged;
    event EventHandler? SessionsChanged;
}

/// <summary>One SMTC session (one media-playing app).</summary>
public interface ISmtcSession
{
    string AppId { get; }

    event EventHandler? MediaPropertiesChanged;
    event EventHandler? PlaybackInfoChanged;
    event EventHandler? TimelinePropertiesChanged;

    Task<SmtcMediaProperties?> TryGetMediaPropertiesAsync();
    PlaybackState GetPlaybackState();
    (TimeSpan Position, TimeSpan Duration) GetTimeline();

    Task<bool> TryPlayPauseAsync();
    Task<bool> TrySkipNextAsync();
    Task<bool> TrySkipPreviousAsync();
}

/// <summary>Raw metadata from a session; art already converted to bytes.</summary>
public sealed record SmtcMediaProperties(
    string? Title,
    string? Artist,
    string? AlbumTitle,
    byte[]? AlbumArtPng);
