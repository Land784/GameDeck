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

    /// <summary>Maps user settings onto timings: 0 s means never auto-hide,
    /// disabled animations collapse the fades to instant.</summary>
    public static OverlayTimings FromSettings(int autoHideSeconds, bool animationsEnabled)
    {
        var visible = autoHideSeconds <= 0 ? Timeout.InfiniteTimeSpan : TimeSpan.FromSeconds(autoHideSeconds);
        return animationsEnabled
            ? Default with { VisibleDuration = visible }
            : new OverlayTimings(TimeSpan.Zero, visible, TimeSpan.Zero);
    }
}

/// <summary>
/// The overlay's auto-hide brain. All timing lives here, driven by
/// <see cref="TimeProvider"/>, so the WPF window is a dumb adapter that maps
/// states to animations. Inputs may come from any thread (the internal timer
/// fires on the threadpool); transitions are serialized internally and
/// <see cref="StateChanged"/> is raised synchronously on whichever thread
/// caused the transition.
/// </summary>
public sealed class OverlayStateMachine : IDisposable
{
    private readonly TimeProvider _time;
    private OverlayTimings _timings;
    private readonly ITimer _timer;
    private readonly object _sync = new();

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

    /// <summary>Countdown to auto-hide; suspended while interactive or an ad is playing.</summary>
    private TimeSpan VisibleTimeout =>
        IsInteractive || _adActive ? Timeout.InfiniteTimeSpan : _timings.VisibleDuration;

    private bool _adActive;

    /// <summary>An ad keeps the overlay up; its end restarts the normal countdown.</summary>
    public void NotifyAdStateChanged(bool adActive)
    {
        lock (_sync)
        {
            _adActive = adActive;
            switch (State)
            {
                case OverlayState.Visible:
                    _timer.Change(VisibleTimeout, Timeout.InfiniteTimeSpan);
                    break;
                case OverlayState.Hidden:
                case OverlayState.FadingOut:
                    if (adActive)
                        TransitionTo(OverlayState.FadingIn, _timings.FadeIn);
                    break;
            }
        }
    }

    /// <summary>Settings changed; a running visible countdown restarts under the new timings.</summary>
    public void UpdateTimings(OverlayTimings timings)
    {
        lock (_sync)
        {
            _timings = timings;
            if (State == OverlayState.Visible)
                _timer.Change(VisibleTimeout, Timeout.InfiniteTimeSpan);
        }
    }

    public void NotifyTrackChanged()
    {
        lock (_sync)
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
    }

    public void SetInteractive(bool interactive)
    {
        lock (_sync)
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
    }

    public void PointerEntered()
    {
        lock (_sync)
        {
            if (State == OverlayState.Visible)
                _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }
    }

    public void PointerExited()
    {
        lock (_sync)
        {
            if (State == OverlayState.Visible)
                _timer.Change(VisibleTimeout, Timeout.InfiniteTimeSpan);
        }
    }

    public void ToggleVisibility()
    {
        lock (_sync)
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
    }

    private void OnTimerFired()
    {
        lock (_sync)
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
    }

    private void TransitionTo(OverlayState state, TimeSpan nextPhaseIn)
    {
        State = state;
        _timer.Change(nextPhaseIn, Timeout.InfiniteTimeSpan);
        StateChanged?.Invoke(this, state);
    }

    public void Dispose() => _timer.Dispose();
}
