namespace GameDeck.Core.Media;

/// <summary>
/// Playback progress, separate from <see cref="MediaSnapshot"/> so coarse
/// timeline ticks (some apps update only every ~5 s) never fire
/// SnapshotChanged. Consumers poll <see cref="IMediaSessionService.CurrentTimeline"/>,
/// which interpolates between real updates while playing.
/// </summary>
public sealed record MediaTimeline(TimeSpan Position, TimeSpan Duration);
