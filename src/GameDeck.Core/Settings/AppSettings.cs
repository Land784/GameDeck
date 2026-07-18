using GameDeck.Core.Hotkeys;

namespace GameDeck.Core.Settings;

public sealed class AppSettings
{
    public int Version { get; set; } = 1;

    // Note: "start with Windows" is deliberately NOT here. The HKCU Run key
    // is the single source of truth; mirroring it would drift.

    /// <summary>Pinned media source app id; null follows the system current session.</summary>
    public string? PreferredAppId { get; set; }

    public List<HotkeyBinding> Hotkeys { get; set; } = HotkeyBinding.Defaults.ToList();

    /// <summary>
    /// Shared secret the browser extension must present in its hello frame.
    /// Generated on first load (additive field; no settings version bump).
    /// </summary>
    public string? BridgeToken { get; set; }

    // Overlay settings land in Phase 2; version bump + migration when they do.
}
