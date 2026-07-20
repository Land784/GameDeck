namespace GameDeck.Core.Hotkeys;

/// <summary>
/// Combo matching and duplicate suppression for the low-level keyboard
/// hook fallback (issue #3: some raw-input exclusive-fullscreen games
/// swallow RegisterHotKey). Both delivery paths report here; an action
/// fires at most once per <see cref="DedupeWindow"/> regardless of which
/// path saw it first. Safe to call from the hook thread and the UI
/// thread concurrently.
/// </summary>
public sealed class HotkeyFallbackMatcher
{
    public static readonly TimeSpan DedupeWindow = TimeSpan.FromMilliseconds(200);

    private readonly TimeProvider _time;
    private readonly object _sync = new();
    private readonly HashSet<uint> _pressed = new();
    private readonly Dictionary<HotkeyAction, long> _lastFired = new();
    private IReadOnlyList<HotkeyBinding> _bindings = Array.Empty<HotkeyBinding>();

    public HotkeyFallbackMatcher(TimeProvider? time = null) =>
        _time = time ?? TimeProvider.System;

    public void SetBindings(IEnumerable<HotkeyBinding> bindings)
    {
        lock (_sync)
        {
            _bindings = bindings.ToList();
        }
    }

    /// <summary>
    /// Key-down seen by the hook. Returns the action to dispatch, or null
    /// for no match, an auto-repeat, or a duplicate of a recent fire.
    /// </summary>
    public HotkeyAction? OnKeyDown(uint vk, HotkeyModifiers modifiers)
    {
        lock (_sync)
        {
            if (!_pressed.Add(vk))
                return null; // Held key auto-repeat.

            var binding = _bindings.FirstOrDefault(b => b.VirtualKey == vk && b.Modifiers == modifiers);
            if (binding is null || !TryFire(binding.Action))
                return null;
            return binding.Action;
        }
    }

    public void OnKeyUp(uint vk)
    {
        lock (_sync)
        {
            _pressed.Remove(vk);
        }
    }

    /// <summary>
    /// WM_HOTKEY arrived for an action. Returns whether to dispatch it
    /// (false when the hook already fired it within the window).
    /// </summary>
    public bool OnRegisteredHotkey(HotkeyAction action)
    {
        lock (_sync)
        {
            return TryFire(action);
        }
    }

    private bool TryFire(HotkeyAction action)
    {
        var now = _time.GetTimestamp();
        if (_lastFired.TryGetValue(action, out var last) &&
            _time.GetElapsedTime(last, now) <= DedupeWindow)
        {
            return false;
        }
        _lastFired[action] = now;
        return true;
    }
}
