using GameDeck.Core.Overlay;
using Microsoft.Extensions.Time.Testing;

namespace GameDeck.Core.Tests;

public class OverlayStateMachineTests
{
    private readonly FakeTimeProvider _time = new();

    private OverlayStateMachine Create(OverlayTimings? timings = null) =>
        new(_time, timings ?? OverlayTimings.Default);

    [Fact]
    public void StartsHidden()
    {
        var machine = Create();

        Assert.Equal(OverlayState.Hidden, machine.State);
    }

    [Fact]
    public void TrackChange_FadesInThenBecomesVisible()
    {
        var machine = Create();
        var states = new List<OverlayState>();
        machine.StateChanged += (_, s) => states.Add(s);

        machine.NotifyTrackChanged();

        Assert.Equal(OverlayState.FadingIn, machine.State);

        _time.Advance(OverlayTimings.Default.FadeIn);

        Assert.Equal(OverlayState.Visible, machine.State);
        Assert.Equal(new[] { OverlayState.FadingIn, OverlayState.Visible }, states);
    }

    [Fact]
    public void AutoHides_AfterVisibleDurationElapses()
    {
        var machine = Create();
        machine.NotifyTrackChanged();
        _time.Advance(OverlayTimings.Default.FadeIn);

        _time.Advance(OverlayTimings.Default.VisibleDuration);

        Assert.Equal(OverlayState.FadingOut, machine.State);

        _time.Advance(OverlayTimings.Default.FadeOut);

        Assert.Equal(OverlayState.Hidden, machine.State);
    }

    [Fact]
    public void TrackChangeWhileVisible_RestartsCountdownWithoutReFading()
    {
        var machine = Create();
        machine.NotifyTrackChanged();
        _time.Advance(OverlayTimings.Default.FadeIn);
        _time.Advance(OverlayTimings.Default.VisibleDuration - TimeSpan.FromSeconds(1));

        var states = new List<OverlayState>();
        machine.StateChanged += (_, s) => states.Add(s);
        machine.NotifyTrackChanged();

        // The original deadline passes; the restarted countdown keeps it visible.
        _time.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(OverlayState.Visible, machine.State);
        Assert.Empty(states);

        _time.Advance(OverlayTimings.Default.VisibleDuration - TimeSpan.FromSeconds(1));
        Assert.Equal(OverlayState.FadingOut, machine.State);
    }

    [Fact]
    public void TrackChangeWhileFadingOut_Reappears()
    {
        var machine = Create();
        machine.NotifyTrackChanged();
        _time.Advance(OverlayTimings.Default.FadeIn);
        _time.Advance(OverlayTimings.Default.VisibleDuration);
        Assert.Equal(OverlayState.FadingOut, machine.State);

        machine.NotifyTrackChanged();

        Assert.Equal(OverlayState.FadingIn, machine.State);
        _time.Advance(OverlayTimings.Default.FadeIn);
        Assert.Equal(OverlayState.Visible, machine.State);
    }

    [Fact]
    public void Toggle_ShowsFromHidden_AndAutoHidesLikeNormal()
    {
        var machine = Create();

        machine.ToggleVisibility();

        Assert.Equal(OverlayState.FadingIn, machine.State);
        _time.Advance(OverlayTimings.Default.FadeIn);
        _time.Advance(OverlayTimings.Default.VisibleDuration);
        Assert.Equal(OverlayState.FadingOut, machine.State);
    }

    [Fact]
    public void Toggle_HidesWhileShowing()
    {
        var machine = Create();
        machine.NotifyTrackChanged();
        _time.Advance(OverlayTimings.Default.FadeIn);
        Assert.Equal(OverlayState.Visible, machine.State);

        machine.ToggleVisibility();

        Assert.Equal(OverlayState.FadingOut, machine.State);
        _time.Advance(OverlayTimings.Default.FadeOut);
        Assert.Equal(OverlayState.Hidden, machine.State);
    }

    [Fact]
    public void Hover_PausesAutoHide_UntilPointerLeaves()
    {
        var machine = Create();
        machine.NotifyTrackChanged();
        _time.Advance(OverlayTimings.Default.FadeIn);

        machine.PointerEntered();
        _time.Advance(OverlayTimings.Default.VisibleDuration + TimeSpan.FromMinutes(5));

        Assert.Equal(OverlayState.Visible, machine.State);

        machine.PointerExited();
        _time.Advance(OverlayTimings.Default.VisibleDuration);

        Assert.Equal(OverlayState.FadingOut, machine.State);
    }

    [Fact]
    public void InteractiveMode_ShowsAndSuspendsAutoHide_UntilTurnedOff()
    {
        var machine = Create();

        machine.SetInteractive(true);

        Assert.Equal(OverlayState.FadingIn, machine.State);
        _time.Advance(OverlayTimings.Default.FadeIn);
        _time.Advance(TimeSpan.FromMinutes(10));
        Assert.Equal(OverlayState.Visible, machine.State);

        // Track changes while interactive must not arm the hide timer.
        machine.NotifyTrackChanged();
        _time.Advance(TimeSpan.FromMinutes(10));
        Assert.Equal(OverlayState.Visible, machine.State);

        machine.SetInteractive(false);
        _time.Advance(OverlayTimings.Default.VisibleDuration);

        Assert.Equal(OverlayState.FadingOut, machine.State);
    }

    [Fact]
    public void InfiniteVisibleDuration_NeverAutoHides()
    {
        var machine = Create(OverlayTimings.Default with { VisibleDuration = Timeout.InfiniteTimeSpan });
        machine.NotifyTrackChanged();
        _time.Advance(OverlayTimings.Default.FadeIn);

        _time.Advance(TimeSpan.FromHours(2));

        Assert.Equal(OverlayState.Visible, machine.State);
    }

    [Fact]
    public void AdAppears_ShowsOverlayAndSuspendsAutoHide()
    {
        var machine = Create();

        machine.NotifyAdStateChanged(adActive: true);

        Assert.Equal(OverlayState.FadingIn, machine.State);
        _time.Advance(OverlayTimings.Default.FadeIn);
        _time.Advance(TimeSpan.FromMinutes(10));
        Assert.Equal(OverlayState.Visible, machine.State);
    }

    [Fact]
    public void AdEnds_RestartsTheAutoHideCountdown()
    {
        var machine = Create();
        machine.NotifyAdStateChanged(adActive: true);
        _time.Advance(OverlayTimings.Default.FadeIn);

        machine.NotifyAdStateChanged(adActive: false);
        _time.Advance(OverlayTimings.Default.VisibleDuration);

        Assert.Equal(OverlayState.FadingOut, machine.State);
    }

    [Fact]
    public void AdAppearsWhileVisible_KeepsOverlayUpWithoutReFading()
    {
        var machine = Create();
        machine.NotifyTrackChanged();
        _time.Advance(OverlayTimings.Default.FadeIn);

        var states = new List<OverlayState>();
        machine.StateChanged += (_, s) => states.Add(s);
        machine.NotifyAdStateChanged(adActive: true);
        _time.Advance(TimeSpan.FromMinutes(10));

        Assert.Equal(OverlayState.Visible, machine.State);
        Assert.Empty(states);
    }

    [Fact]
    public void AdEndsWhileHidden_StaysHidden()
    {
        var machine = Create();

        machine.NotifyAdStateChanged(adActive: false);
        _time.Advance(TimeSpan.FromMinutes(1));

        Assert.Equal(OverlayState.Hidden, machine.State);
    }

    [Fact]
    public void RapidToggleMidFade_DoesNotFireStaleDeadlines()
    {
        var machine = Create();
        machine.NotifyTrackChanged();
        _time.Advance(TimeSpan.FromMilliseconds(100)); // mid fade-in

        machine.ToggleVisibility(); // now fading out (300 ms)
        _time.Advance(TimeSpan.FromMilliseconds(60)); // old fade-in deadline passes

        Assert.Equal(OverlayState.FadingOut, machine.State);

        _time.Advance(TimeSpan.FromMilliseconds(240)); // full fade-out elapses

        Assert.Equal(OverlayState.Hidden, machine.State);
    }
}
