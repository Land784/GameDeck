namespace GameDeck.Core.Media;

/// <summary>A media source available through SMTC (e.g. Spotify, a browser).</summary>
public sealed record SessionInfo(string AppId, string DisplayName);
