using GameDeck.Core.Media;
using GameDeck.Core.Tests.Fakes;
using Microsoft.Extensions.Time.Testing;

namespace GameDeck.Core.Tests;

public class MediaSessionServiceTests
{
    private static readonly TimeSpan PastDebounce = MediaSessionService.DebounceInterval + TimeSpan.FromMilliseconds(10);

    private readonly FakeSmtcFacade _facade = new();
    private readonly FakeTimeProvider _time = new();

    private async Task<MediaSessionService> CreateInitializedAsync()
    {
        var service = new MediaSessionService(_facade, _time);
        await service.InitializeAsync();
        _time.Advance(PastDebounce); // flush the initial snapshot
        return service;
    }

    private static FakeSmtcSession Session(string appId, string title = "Song") =>
        new(appId) { Title = title, Artist = "Artist", Album = "Album" };

    // --- Session selection policy ---

    [Fact]
    public async Task NullPreferred_FollowsSystemCurrentSession()
    {
        var spotify = Session("Spotify.exe");
        _facade.FakeSessions.Add(spotify);
        _facade.FakeCurrent = spotify;

        var service = await CreateInitializedAsync();

        Assert.Equal("Spotify.exe", service.Current?.SourceAppId);
    }

    [Fact]
    public async Task PreferredPresent_PinsToThatSession()
    {
        var spotify = Session("Spotify.exe", "SpotifySong");
        var edge = Session("MSEdge", "EdgeSong");
        _facade.FakeSessions.AddRange(new[] { spotify, edge });
        _facade.FakeCurrent = spotify;

        var service = await CreateInitializedAsync();
        service.PreferredAppId = "MSEdge";
        _time.Advance(PastDebounce);

        Assert.Equal("EdgeSong", service.Current?.Title);
    }

    [Fact]
    public async Task PreferredMissing_FallsBackToCurrent()
    {
        var spotify = Session("Spotify.exe");
        _facade.FakeSessions.Add(spotify);
        _facade.FakeCurrent = spotify;

        var service = await CreateInitializedAsync();
        service.PreferredAppId = "NotInstalled.exe";
        _time.Advance(PastDebounce);

        Assert.Equal("Spotify.exe", service.Current?.SourceAppId);
    }

    [Fact]
    public async Task PreferredDisappears_FallsBackToCurrentOnSessionsChanged()
    {
        var spotify = Session("Spotify.exe", "SpotifySong");
        var edge = Session("MSEdge", "EdgeSong");
        _facade.FakeSessions.AddRange(new[] { spotify, edge });
        _facade.FakeCurrent = spotify;

        var service = await CreateInitializedAsync();
        service.PreferredAppId = "MSEdge";
        _time.Advance(PastDebounce);
        Assert.Equal("EdgeSong", service.Current?.Title);

        _facade.FakeSessions.Remove(edge);
        _facade.RaiseSessionsChanged();
        _time.Advance(PastDebounce);

        Assert.Equal("SpotifySong", service.Current?.Title);
    }

    [Fact]
    public async Task NoSessions_SnapshotIsNull()
    {
        var service = await CreateInitializedAsync();

        Assert.Null(service.Current);
    }

    [Fact]
    public async Task CurrentSessionCloses_SnapshotGoesNullAndRaises()
    {
        var spotify = Session("Spotify.exe");
        _facade.FakeSessions.Add(spotify);
        _facade.FakeCurrent = spotify;
        var service = await CreateInitializedAsync();

        MediaSnapshot? received = new MediaSnapshot("x", "x", "x", PlaybackState.Playing, "x");
        service.SnapshotChanged += (_, s) => received = s;

        _facade.FakeSessions.Clear();
        _facade.FakeCurrent = null;
        _facade.RaiseCurrentSessionChanged();
        _time.Advance(PastDebounce);

        Assert.Null(received);
        Assert.Null(service.Current);
    }

    // --- Debounce ---

    [Fact]
    public async Task EventBurst_CoalescesIntoSingleSnapshotChange()
    {
        var spotify = Session("Spotify.exe");
        _facade.FakeSessions.Add(spotify);
        _facade.FakeCurrent = spotify;
        var service = await CreateInitializedAsync();

        var raised = 0;
        service.SnapshotChanged += (_, _) => raised++;

        spotify.Title = "New Song";
        for (var i = 0; i < 5; i++)
        {
            spotify.RaiseMediaPropertiesChanged();
            spotify.RaisePlaybackInfoChanged();
            _time.Advance(TimeSpan.FromMilliseconds(20));
        }

        _time.Advance(PastDebounce);

        Assert.Equal(1, raised);
        Assert.Equal("New Song", service.Current?.Title);
    }

    [Fact]
    public async Task EventsSpacedBeyondDebounce_RaiseSeparately()
    {
        var spotify = Session("Spotify.exe");
        _facade.FakeSessions.Add(spotify);
        _facade.FakeCurrent = spotify;
        var service = await CreateInitializedAsync();

        var raised = 0;
        service.SnapshotChanged += (_, _) => raised++;

        spotify.Title = "Song A";
        spotify.RaiseMediaPropertiesChanged();
        _time.Advance(PastDebounce);

        spotify.Title = "Song B";
        spotify.RaiseMediaPropertiesChanged();
        _time.Advance(PastDebounce);

        Assert.Equal(2, raised);
    }

    [Fact]
    public async Task NoEventBeforeDebounceElapses()
    {
        var spotify = Session("Spotify.exe");
        _facade.FakeSessions.Add(spotify);
        _facade.FakeCurrent = spotify;
        var service = await CreateInitializedAsync();

        var raised = 0;
        service.SnapshotChanged += (_, _) => raised++;

        spotify.Title = "New Song";
        spotify.RaiseMediaPropertiesChanged();
        _time.Advance(TimeSpan.FromMilliseconds(100));

        Assert.Equal(0, raised);
    }

    // --- Snapshot equality gating ---

    [Fact]
    public async Task UnchangedMetadata_RaisesNoEvent()
    {
        var spotify = Session("Spotify.exe");
        _facade.FakeSessions.Add(spotify);
        _facade.FakeCurrent = spotify;
        var service = await CreateInitializedAsync();

        var raised = 0;
        service.SnapshotChanged += (_, _) => raised++;

        spotify.RaiseMediaPropertiesChanged(); // nothing actually changed
        _time.Advance(PastDebounce);

        Assert.Equal(0, raised);
    }

    [Fact]
    public async Task SameArtBytesInNewArray_RaisesNoEvent()
    {
        var spotify = Session("Spotify.exe");
        spotify.Art = new byte[] { 1, 2, 3 };
        _facade.FakeSessions.Add(spotify);
        _facade.FakeCurrent = spotify;
        var service = await CreateInitializedAsync();

        var raised = 0;
        service.SnapshotChanged += (_, _) => raised++;

        spotify.Art = new byte[] { 1, 2, 3 }; // new array, same content
        spotify.RaiseMediaPropertiesChanged();
        _time.Advance(PastDebounce);

        Assert.Equal(0, raised);
    }

    [Fact]
    public async Task TitleChange_RaisesWithNewSnapshot()
    {
        var spotify = Session("Spotify.exe", "Old");
        _facade.FakeSessions.Add(spotify);
        _facade.FakeCurrent = spotify;
        var service = await CreateInitializedAsync();

        MediaSnapshot? received = null;
        service.SnapshotChanged += (_, s) => received = s;

        spotify.Title = "New";
        spotify.RaiseMediaPropertiesChanged();
        _time.Advance(PastDebounce);

        Assert.Equal("New", received?.Title);
        Assert.Equal(PlaybackState.Playing, received?.Playback);
    }

    // --- Session switch subscription hygiene ---

    [Fact]
    public async Task SessionSwitch_UnsubscribesFromOldSession()
    {
        var spotify = Session("Spotify.exe");
        var edge = Session("MSEdge");
        _facade.FakeSessions.AddRange(new[] { spotify, edge });
        _facade.FakeCurrent = spotify;
        var service = await CreateInitializedAsync();

        Assert.Equal(1, spotify.MediaPropertiesSubscriberCount);

        _facade.FakeCurrent = edge;
        _facade.RaiseCurrentSessionChanged();
        _time.Advance(PastDebounce);

        Assert.Equal(0, spotify.MediaPropertiesSubscriberCount);
        Assert.Equal(1, edge.MediaPropertiesSubscriberCount);
    }

    [Fact]
    public async Task RepeatedTopologyEvents_DoNotStackHandlers()
    {
        var spotify = Session("Spotify.exe");
        _facade.FakeSessions.Add(spotify);
        _facade.FakeCurrent = spotify;
        var service = await CreateInitializedAsync();

        for (var i = 0; i < 5; i++)
            _facade.RaiseSessionsChanged();
        _time.Advance(PastDebounce);

        Assert.Equal(1, spotify.MediaPropertiesSubscriberCount);
    }

    // --- Commands ---

    [Fact]
    public async Task Commands_RouteToSelectedSession()
    {
        var spotify = Session("Spotify.exe");
        var edge = Session("MSEdge");
        _facade.FakeSessions.AddRange(new[] { spotify, edge });
        _facade.FakeCurrent = spotify;
        var service = await CreateInitializedAsync();
        service.PreferredAppId = "MSEdge";

        await service.PlayPauseAsync();
        await service.NextAsync();
        await service.PreviousAsync();

        Assert.Equal(new[] { "playpause", "next", "previous" }, edge.Commands);
        Assert.Empty(spotify.Commands);
    }

    [Fact]
    public async Task Commands_NoSession_DoNotThrow()
    {
        var service = await CreateInitializedAsync();

        await service.PlayPauseAsync();
        await service.NextAsync();
        await service.PreviousAsync();
    }

    [Fact]
    public async Task Commands_SessionThrows_IsSwallowed()
    {
        var spotify = Session("Spotify.exe");
        spotify.ThrowOnCommand = true;
        _facade.FakeSessions.Add(spotify);
        _facade.FakeCurrent = spotify;
        var service = await CreateInitializedAsync();

        await service.NextAsync(); // must not propagate
    }

    // --- Timeline ---

    [Fact]
    public async Task TimelineTick_DoesNotRaiseSnapshotChanged()
    {
        var spotify = Session("Spotify.exe");
        spotify.State = PlaybackState.Paused; // freeze interpolation for a stable assert
        _facade.FakeSessions.Add(spotify);
        _facade.FakeCurrent = spotify;
        var service = await CreateInitializedAsync();

        var raised = 0;
        service.SnapshotChanged += (_, _) => raised++;

        spotify.Position = TimeSpan.FromSeconds(42);
        spotify.RaiseTimelinePropertiesChanged();
        _time.Advance(PastDebounce);

        Assert.Equal(0, raised);
        Assert.Equal(TimeSpan.FromSeconds(42), service.CurrentTimeline?.Position);
    }

    [Fact]
    public async Task CurrentTimeline_InterpolatesWhilePlaying()
    {
        var spotify = Session("Spotify.exe");
        spotify.State = PlaybackState.Playing;
        spotify.Position = TimeSpan.FromSeconds(10);
        spotify.Duration = TimeSpan.FromSeconds(60);
        _facade.FakeSessions.Add(spotify);
        _facade.FakeCurrent = spotify;
        var service = await CreateInitializedAsync();

        _time.Advance(TimeSpan.FromSeconds(5));

        Assert.Equal(TimeSpan.FromSeconds(15), service.CurrentTimeline?.Position);
        Assert.Equal(TimeSpan.FromSeconds(60), service.CurrentTimeline?.Duration);
    }

    [Fact]
    public async Task CurrentTimeline_FrozenWhilePaused()
    {
        var spotify = Session("Spotify.exe");
        spotify.State = PlaybackState.Paused;
        spotify.Position = TimeSpan.FromSeconds(10);
        spotify.Duration = TimeSpan.FromSeconds(60);
        _facade.FakeSessions.Add(spotify);
        _facade.FakeCurrent = spotify;
        var service = await CreateInitializedAsync();

        _time.Advance(TimeSpan.FromSeconds(5));

        Assert.Equal(TimeSpan.FromSeconds(10), service.CurrentTimeline?.Position);
    }

    [Fact]
    public async Task CurrentTimeline_ClampsToDuration()
    {
        var spotify = Session("Spotify.exe");
        spotify.State = PlaybackState.Playing;
        spotify.Position = TimeSpan.FromSeconds(55);
        spotify.Duration = TimeSpan.FromSeconds(60);
        _facade.FakeSessions.Add(spotify);
        _facade.FakeCurrent = spotify;
        var service = await CreateInitializedAsync();

        _time.Advance(TimeSpan.FromSeconds(30));

        Assert.Equal(TimeSpan.FromSeconds(60), service.CurrentTimeline?.Position);
    }

    [Fact]
    public async Task CurrentTimeline_NullWhenNoSession()
    {
        var service = await CreateInitializedAsync();

        Assert.Null(service.CurrentTimeline);
    }

    // --- Refresh (unlock recovery) ---

    [Fact]
    public async Task Refresh_ReattachesWithoutAnyFacadeEvent()
    {
        var spotify = Session("Spotify.exe", "SpotifySong");
        _facade.FakeSessions.Add(spotify);
        _facade.FakeCurrent = spotify;
        var service = await CreateInitializedAsync();

        // Simulate the facade state changing while its events were silently
        // dropped (e.g. across a lock/unlock).
        var edge = Session("MSEdge", "EdgeSong");
        _facade.FakeSessions.Add(edge);
        _facade.FakeCurrent = edge;

        service.Refresh();
        _time.Advance(PastDebounce);

        Assert.Equal("EdgeSong", service.Current?.Title);
        Assert.Equal(0, spotify.MediaPropertiesSubscriberCount);
        Assert.Equal(1, edge.MediaPropertiesSubscriberCount);
    }

    // --- Sessions listing ---

    [Fact]
    public async Task Sessions_ExposeFriendlyNames()
    {
        _facade.FakeSessions.Add(Session("Spotify.exe"));
        _facade.FakeSessions.Add(Session("MSEdge"));
        var service = await CreateInitializedAsync();

        var sessions = service.Sessions;

        Assert.Equal(2, sessions.Count);
        Assert.Equal("Spotify", sessions[0].DisplayName);
        Assert.Equal("MSEdge", sessions[1].DisplayName);
    }
}
