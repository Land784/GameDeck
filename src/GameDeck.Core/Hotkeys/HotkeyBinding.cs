namespace GameDeck.Core.Hotkeys;

/// <summary>Matches the Win32 MOD_* values used by RegisterHotKey.</summary>
[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 0x1,
    Control = 0x2,
    Shift = 0x4,
    Win = 0x8,
}

/// <summary><paramref name="VirtualKey"/> is a Win32 VK code.</summary>
public sealed record HotkeyBinding(HotkeyAction Action, HotkeyModifiers Modifiers, uint VirtualKey)
{
    public static class Vk
    {
        public const uint Space = 0x20;
        public const uint Left = 0x25;
        public const uint Right = 0x27;
        public const uint I = 0x49;
        public const uint O = 0x4F;
        public const uint S = 0x53;
    }

    public static IReadOnlyList<HotkeyBinding> Defaults { get; } = new[]
    {
        new HotkeyBinding(HotkeyAction.PlayPause, HotkeyModifiers.Control | HotkeyModifiers.Alt, Vk.Space),
        new HotkeyBinding(HotkeyAction.NextTrack, HotkeyModifiers.Control | HotkeyModifiers.Alt, Vk.Right),
        new HotkeyBinding(HotkeyAction.PreviousTrack, HotkeyModifiers.Control | HotkeyModifiers.Alt, Vk.Left),
        new HotkeyBinding(HotkeyAction.ToggleOverlay, HotkeyModifiers.Control | HotkeyModifiers.Alt, Vk.O),
        new HotkeyBinding(HotkeyAction.ToggleInteractivity, HotkeyModifiers.Control | HotkeyModifiers.Alt, Vk.I),
        new HotkeyBinding(HotkeyAction.SkipAd, HotkeyModifiers.Control | HotkeyModifiers.Alt, Vk.S),
    };
}
