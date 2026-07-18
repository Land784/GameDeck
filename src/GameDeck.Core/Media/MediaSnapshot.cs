namespace GameDeck.Core.Media;

/// <summary>
/// Immutable view of what's playing right now. Value equality includes album
/// art bytes so consumers can rely on "no event unless something changed".
/// Progress lives in <see cref="MediaTimeline"/>, not here — position ticks
/// must not look like track changes.
/// </summary>
public sealed record MediaSnapshot(
    string Title,
    string Artist,
    string AlbumTitle,
    PlaybackState Playback,
    string SourceAppId)
{
    public byte[]? AlbumArtPng { get; init; }

    public bool Equals(MediaSnapshot? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Title == other.Title
               && Artist == other.Artist
               && AlbumTitle == other.AlbumTitle
               && Playback == other.Playback
               && SourceAppId == other.SourceAppId
               && ArtEquals(AlbumArtPng, other.AlbumArtPng);
    }

    public override int GetHashCode() =>
        HashCode.Combine(Title, Artist, AlbumTitle, Playback, SourceAppId,
            AlbumArtPng?.Length ?? -1);

    private static bool ArtEquals(byte[]? a, byte[]? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;
        return a.AsSpan().SequenceEqual(b);
    }
}
