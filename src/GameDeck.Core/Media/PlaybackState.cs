namespace GameDeck.Core.Media;

/// <summary>Playback status, mirroring SMTC's states without exposing WinRT types.</summary>
public enum PlaybackState
{
    Unknown,
    Closed,
    Opened,
    Changing,
    Stopped,
    Playing,
    Paused,
}
