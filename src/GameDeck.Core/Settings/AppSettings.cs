using GameDeck.Core.Hotkeys;

namespace GameDeck.Core.Settings;

public sealed class AppSettings
{
    public int Version { get; set; } = 1;

    public bool StartWithWindows { get; set; }

    /// <summary>Pinned media source app id; null follows the system current session.</summary>
    public string? PreferredAppId { get; set; }

    public List<HotkeyBinding> Hotkeys { get; set; } = HotkeyBinding.Defaults.ToList();

    // Overlay settings land in Phase 2; version bump + migration when they do.
}
