using GameDeck.Core.Hotkeys;

namespace GameDeck.Core.Tests;

public class HotkeyBindingTests
{
    [Fact]
    public void Defaults_CoverEveryAction()
    {
        var bound = HotkeyBinding.Defaults.Select(b => b.Action).ToHashSet();

        foreach (var action in Enum.GetValues<HotkeyAction>())
            Assert.Contains(action, bound);
    }

    [Fact]
    public void Defaults_HaveNoDuplicateCombos()
    {
        var combos = HotkeyBinding.Defaults.Select(b => (b.Modifiers, b.VirtualKey)).ToList();

        Assert.Equal(combos.Count, combos.Distinct().Count());
    }

    [Fact]
    public void ActionValues_AreStable()
    {
        // These double as RegisterHotKey ids; renumbering breaks re-registration.
        Assert.Equal(1, (int)HotkeyAction.PlayPause);
        Assert.Equal(2, (int)HotkeyAction.NextTrack);
        Assert.Equal(3, (int)HotkeyAction.PreviousTrack);
        Assert.Equal(4, (int)HotkeyAction.ToggleOverlay);
        Assert.Equal(5, (int)HotkeyAction.ToggleInteractivity);
        Assert.Equal(6, (int)HotkeyAction.SkipAd);
    }
}
