using GameDeck.Core.Media;

namespace GameDeck.Core.Tests.Fakes;

public sealed class FakeSmtcFacade : ISmtcFacade
{
    public List<FakeSmtcSession> FakeSessions { get; } = new();
    public FakeSmtcSession? FakeCurrent { get; set; }
    public bool Initialized { get; private set; }

    public ISmtcSession? CurrentSession => FakeCurrent;
    public IReadOnlyList<ISmtcSession> Sessions => FakeSessions;

    public event EventHandler? CurrentSessionChanged;
    public event EventHandler? SessionsChanged;

    public Task InitializeAsync()
    {
        Initialized = true;
        return Task.CompletedTask;
    }

    public void RaiseCurrentSessionChanged() => CurrentSessionChanged?.Invoke(this, EventArgs.Empty);
    public void RaiseSessionsChanged() => SessionsChanged?.Invoke(this, EventArgs.Empty);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

public sealed class FakeSmtcSession : ISmtcSession
{
    public FakeSmtcSession(string appId) => AppId = appId;

    public string AppId { get; }

    public string? Title { get; set; }
    public string? Artist { get; set; }
    public string? Album { get; set; }
    public byte[]? Art { get; set; }
    public PlaybackState State { get; set; } = PlaybackState.Playing;
    public TimeSpan Position { get; set; }
    public TimeSpan Duration { get; set; }

    public bool CommandResult { get; set; } = true;
    public bool ThrowOnCommand { get; set; }
    public List<string> Commands { get; } = new();

    public event EventHandler? MediaPropertiesChanged;
    public event EventHandler? PlaybackInfoChanged;
    public event EventHandler? TimelinePropertiesChanged;

    public int MediaPropertiesSubscriberCount =>
        MediaPropertiesChanged?.GetInvocationList().Length ?? 0;

    public void RaiseMediaPropertiesChanged() => MediaPropertiesChanged?.Invoke(this, EventArgs.Empty);
    public void RaisePlaybackInfoChanged() => PlaybackInfoChanged?.Invoke(this, EventArgs.Empty);
    public void RaiseTimelinePropertiesChanged() => TimelinePropertiesChanged?.Invoke(this, EventArgs.Empty);

    public Task<SmtcMediaProperties?> TryGetMediaPropertiesAsync() =>
        Task.FromResult<SmtcMediaProperties?>(new SmtcMediaProperties(Title, Artist, Album, Art));

    public PlaybackState GetPlaybackState() => State;

    public (TimeSpan Position, TimeSpan Duration) GetTimeline() => (Position, Duration);

    public Task<bool> TryPlayPauseAsync() => RunCommand("playpause");
    public Task<bool> TrySkipNextAsync() => RunCommand("next");
    public Task<bool> TrySkipPreviousAsync() => RunCommand("previous");

    private Task<bool> RunCommand(string name)
    {
        if (ThrowOnCommand) throw new InvalidOperationException("SMTC rejected the command");
        Commands.Add(name);
        return Task.FromResult(CommandResult);
    }
}
