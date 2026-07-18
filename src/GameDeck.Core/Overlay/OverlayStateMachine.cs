namespace GameDeck.Core.Overlay;

public enum OverlayState
{
    Hidden,
    FadingIn,
    Visible,
    FadingOut,
}

/// <summary>
/// Durations for the overlay show/hide cycle. An infinite
/// <paramref name="VisibleDuration"/> means "always visible" (never auto-hide).
/// </summary>
public sealed record OverlayTimings(TimeSpan FadeIn, TimeSpan VisibleDuration, TimeSpan FadeOut)
{
    public static OverlayTimings Default { get; } = new(
        TimeSpan.FromMilliseconds(150),
        TimeSpan.FromSeconds(4),
        TimeSpan.FromMilliseconds(300));
}

/// <summary>
/// The overlay's auto-hide brain. All timing lives here, driven by
/// <see cref="TimeProvider"/>, so the WPF window is a dumb adapter that maps
/// states to animations. Not thread-safe by design intent of callers: drive
/// it from one context (the UI thread); events are raised synchronously.
/// </summary>
public sealed class OverlayStateMachine : IDisposable
{
    private readonly TimeProvider _time;
    private readonly OverlayTimings _timings;
    private readonly ITimer _timer;

    public OverlayStateMachine(TimeProvider time, OverlayTimings timings)
    {
        _time = time;
        _timings = timings;
        _timer = _time.CreateTimer(_ => OnTimerFired(), null,
            Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    public OverlayState State { get; private set; } = OverlayState.Hidden;

    public bool IsInteractive { get; private set; }

    public event EventHandler<OverlayState>? StateChanged;

    /// <summary>Countdown to auto-hide; suspended while interactive.</summary>
    private TimeSpan VisibleTimeout => IsInteractive ? Timeout.InfiniteTimeSpan : _timings.VisibleDuration;

    public void NotifyTrackChanged()
    {
        switch (State)
        {
            case OverlayState.Visible:
                // Already showing: just restart the countdown, no re-fade.
                _timer.Change(VisibleTimeout, Timeout.InfiniteTimeSpan);
                break;
            case OverlayState.FadingIn:
                break; // Already appearing.
            default:
                TransitionTo(OverlayState.FadingIn, _timings.FadeIn);
                break;
        }
    }

    public void SetInteractive(bool interactive)
    {
        IsInteractive = interactive;
        switch (State)
        {
            case OverlayState.Visible:
                _timer.Change(VisibleTimeout, Timeout.InfiniteTimeSpan);
                break;
            case OverlayState.Hidden:
            case OverlayState.FadingOut:
                if (interactive)
                    TransitionTo(OverlayState.FadingIn, _timings.FadeIn);
                break;
        }
    }

    public void PointerEntered()
    {
        if (State == OverlayState.Visible)
            _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    public void PointerExited()
    {
        if (State == OverlayState.Visible)
            _timer.Change(VisibleTimeout, Timeout.InfiniteTimeSpan);
    }

    public void ToggleVisibility()
    {
        switch (State)
        {
            case OverlayState.Hidden:
            case OverlayState.FadingOut:
                TransitionTo(OverlayState.FadingIn, _timings.FadeIn);
                break;
            case OverlayState.FadingIn:
            case OverlayState.Visible:
                TransitionTo(OverlayState.FadingOut, _timings.FadeOut);
                break;
        }
    }

    private void OnTimerFired()
    {
        switch (State)
        {
            case OverlayState.FadingIn:
                TransitionTo(OverlayState.Visible, VisibleTimeout);
                break;
            case OverlayState.Visible:
                TransitionTo(OverlayState.FadingOut, _timings.FadeOut);
                break;
            case OverlayState.FadingOut:
                TransitionTo(OverlayState.Hidden, Timeout.InfiniteTimeSpan);
                break;
        }
    }

    private void TransitionTo(OverlayState state, TimeSpan nextPhaseIn)
    {
        State = state;
        _timer.Change(nextPhaseIn, Timeout.InfiniteTimeSpan);
        StateChanged?.Invoke(this, state);
    }

    public void Dispose() => _timer.Dispose();
}
