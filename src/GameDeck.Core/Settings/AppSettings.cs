using GameDeck.Core.Hotkeys;
using GameDeck.Core.Overlay;

namespace GameDeck.Core.Settings;

public sealed class AppSettings
{
    public int Version { get; set; } = 1;

    // Note: "start with Windows" is deliberately NOT here. The HKCU Run key
    // is the single source of truth; mirroring it would drift.

    /// <summary>Pinned media source app id; null follows the system current session.</summary>
    public string? PreferredAppId { get; set; }

    /// <summary>Display name captured when pinning, so the picker can show
    /// "Spotify (not running)" after the app closes (additive field).</summary>
    public string? PreferredAppName { get; set; }

    public List<HotkeyBinding> Hotkeys { get; set; } = HotkeyBinding.Defaults.ToList();

    /// <summary>
    /// Shared secret the browser extension must present in its hello frame.
    /// Generated on first load (additive field; no settings version bump).
    /// </summary>
    public string? BridgeToken { get; set; }

    // Overlay placement and behavior (all additive; defaults match the
    // pre-settings behavior: top-right, 16 px margin, 4 s auto-hide).

    /// <summary>Monitor device name; null or missing monitor falls back to primary.</summary>
    public string? OverlayMonitor { get; set; }

    public OverlayCorner OverlayCorner { get; set; } = OverlayCorner.TopRight;

    public double OverlayOffsetX { get; set; } = OverlayPlacement.DefaultMargin;

    public double OverlayOffsetY { get; set; } = OverlayPlacement.DefaultMargin;

    /// <summary>0.3 to 1.0.</summary>
    public double OverlayOpacity { get; set; } = 1.0;

    /// <summary>0 means never auto-hide.</summary>
    public int AutoHideSeconds { get; set; } = 4;

    public bool AnimationsEnabled { get; set; } = true;

    // Overlay settings land in Phase 2; version bump + migration when they do.
}
