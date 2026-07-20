using GameDeck.Core.Hotkeys;
using Microsoft.Extensions.Time.Testing;

namespace GameDeck.Core.Tests;

public class HotkeyFallbackMatcherTests
{
    private const HotkeyModifiers CtrlAlt = HotkeyModifiers.Control | HotkeyModifiers.Alt;

    private readonly FakeTimeProvider _time = new();

    private HotkeyFallbackMatcher Create()
    {
        var matcher = new HotkeyFallbackMatcher(_time);
        matcher.SetBindings(HotkeyBinding.Defaults);
        return matcher;
    }

    [Fact]
    public void KeyDown_WithBoundComboExactly_ReturnsAction()
    {
        var matcher = Create();

        var action = matcher.OnKeyDown(HotkeyBinding.Vk.Right, CtrlAlt);

        Assert.Equal(HotkeyAction.NextTrack, action);
    }

    [Fact]
    public void KeyDown_WithExtraModifier_DoesNotMatch()
    {
        var matcher = Create();

        var action = matcher.OnKeyDown(HotkeyBinding.Vk.Right, CtrlAlt | HotkeyModifiers.Shift);

        Assert.Null(action);
    }

    [Fact]
    public void KeyDown_WithMissingModifier_DoesNotMatch()
    {
        var matcher = Create();

        Assert.Null(matcher.OnKeyDown(HotkeyBinding.Vk.Right, HotkeyModifiers.Control));
    }

    [Fact]
    public void KeyDown_UnboundKey_DoesNotMatch()
    {
        var matcher = Create();

        Assert.Null(matcher.OnKeyDown(0x41 /* A */, CtrlAlt));
    }

    [Fact]
    public void HeldKey_AutoRepeatKeyDowns_FireOnlyOnce()
    {
        var matcher = Create();

        Assert.NotNull(matcher.OnKeyDown(HotkeyBinding.Vk.Right, CtrlAlt));
        _time.Advance(TimeSpan.FromSeconds(1));
        Assert.Null(matcher.OnKeyDown(HotkeyBinding.Vk.Right, CtrlAlt));
    }

    [Fact]
    public void PressReleasePress_FiresAgainAfterDedupeWindow()
    {
        var matcher = Create();

        Assert.NotNull(matcher.OnKeyDown(HotkeyBinding.Vk.Right, CtrlAlt));
        matcher.OnKeyUp(HotkeyBinding.Vk.Right);
        _time.Advance(HotkeyFallbackMatcher.DedupeWindow + TimeSpan.FromMilliseconds(1));

        Assert.NotNull(matcher.OnKeyDown(HotkeyBinding.Vk.Right, CtrlAlt));
    }

    [Fact]
    public void RegisteredHotkey_AfterHookFired_IsSuppressedWithinWindow()
    {
        var matcher = Create();

        matcher.OnKeyDown(HotkeyBinding.Vk.Right, CtrlAlt);
        _time.Advance(TimeSpan.FromMilliseconds(10));

        Assert.False(matcher.OnRegisteredHotkey(HotkeyAction.NextTrack));
    }

    [Fact]
    public void HookMatch_AfterRegisteredHotkeyFired_IsSuppressedWithinWindow()
    {
        var matcher = Create();

        Assert.True(matcher.OnRegisteredHotkey(HotkeyAction.NextTrack));
        _time.Advance(TimeSpan.FromMilliseconds(10));

        Assert.Null(matcher.OnKeyDown(HotkeyBinding.Vk.Right, CtrlAlt));
    }

    [Fact]
    public void RegisteredHotkey_OutsideWindow_Dispatches()
    {
        var matcher = Create();

        matcher.OnKeyDown(HotkeyBinding.Vk.Right, CtrlAlt);
        _time.Advance(HotkeyFallbackMatcher.DedupeWindow + TimeSpan.FromMilliseconds(1));

        Assert.True(matcher.OnRegisteredHotkey(HotkeyAction.NextTrack));
    }

    [Fact]
    public void DedupeIsPerAction_OtherActionsUnaffected()
    {
        var matcher = Create();

        matcher.OnKeyDown(HotkeyBinding.Vk.Right, CtrlAlt);
        _time.Advance(TimeSpan.FromMilliseconds(10));

        Assert.True(matcher.OnRegisteredHotkey(HotkeyAction.PlayPause));
    }

    [Fact]
    public void SetBindings_ReplacesTheActiveSet()
    {
        var matcher = Create();
        matcher.SetBindings(new[]
        {
            new HotkeyBinding(HotkeyAction.NextTrack, HotkeyModifiers.Shift, 0x75 /* F6 */),
        });

        Assert.Null(matcher.OnKeyDown(HotkeyBinding.Vk.Right, CtrlAlt));
        Assert.Equal(HotkeyAction.NextTrack, matcher.OnKeyDown(0x75, HotkeyModifiers.Shift));
    }
}
