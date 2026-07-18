using GameDeck.Core.Bridge;
using Microsoft.Extensions.Time.Testing;

namespace GameDeck.Core.Tests;

public class BridgeHubTests
{
    private const string Token = "secret-token";

    private readonly FakeTimeProvider _time = new();
    private readonly AdStateTracker _tracker = new();
    private readonly BridgeHub _hub;

    public BridgeHubTests()
    {
        _hub = new BridgeHub(Token, _tracker, _time);
    }

    private async Task<FakeBridgeConnection> ConnectAndAuthenticateAsync(string id = "conn1")
    {
        var conn = new FakeBridgeConnection(id);
        _hub.OnConnected(conn);
        await _hub.OnFrameAsync(id, $$"""{ "v":1, "type":"hello", "client":"extension", "ext":"1.0.0", "token":"{{Token}}" }""");
        return conn;
    }

    [Fact]
    public async Task HelloWithCorrectToken_ReceivesHelloAck()
    {
        var conn = await ConnectAndAuthenticateAsync();

        Assert.Equal(["""{"v":1,"type":"helloAck"}"""], conn.Sent);
        Assert.False(conn.Closed);
    }

    [Theory]
    [InlineData("""{ "v":1, "type":"hello", "client":"extension", "ext":"1.0.0", "token":"wrong" }""")]
    [InlineData("""{ "v":1, "type":"adState", "tabId":1, "adActive":true }""")]
    [InlineData("garbage")]
    public async Task UnauthenticatedFrameThatIsNotAValidHello_ClosesConnection(string frame)
    {
        var conn = new FakeBridgeConnection("conn1");
        _hub.OnConnected(conn);

        await _hub.OnFrameAsync("conn1", frame);

        Assert.True(conn.Closed);
        Assert.Empty(conn.Sent);
    }

    [Fact]
    public async Task AuthenticatedAdState_ReachesTheTracker()
    {
        await ConnectAndAuthenticateAsync();

        await _hub.OnFrameAsync("conn1",
            """{ "v":1, "type":"adState", "tabId":42, "adActive":true, "skippable":true }""");

        Assert.Equal(new AdStatus("conn1", 42, Skippable: true, SecondsUntilSkippable: null), _tracker.ActiveAd);
    }

    [Fact]
    public async Task Disconnect_ClearsThatConnectionsAdsFromTracker()
    {
        await ConnectAndAuthenticateAsync();
        await _hub.OnFrameAsync("conn1", """{ "v":1, "type":"adState", "tabId":42, "adActive":true }""");

        _hub.OnDisconnected("conn1");

        Assert.Null(_tracker.ActiveAd);
    }

    [Fact]
    public async Task AdStateBeforeAuthentication_NeverReachesTheTracker()
    {
        var conn = new FakeBridgeConnection("conn1");
        _hub.OnConnected(conn);

        await _hub.OnFrameAsync("conn1", """{ "v":1, "type":"adState", "tabId":42, "adActive":true }""");

        Assert.Null(_tracker.ActiveAd);
    }

    [Fact]
    public async Task SendSkip_RoutesToTheConnectionAndTabShowingTheActiveAd()
    {
        var conn1 = await ConnectAndAuthenticateAsync("conn1");
        var conn2 = await ConnectAndAuthenticateAsync("conn2");
        await _hub.OnFrameAsync("conn1", """{ "v":1, "type":"adState", "tabId":7, "adActive":true }""");
        await _hub.OnFrameAsync("conn2", """{ "v":1, "type":"adState", "tabId":9, "adActive":true, "skippable":true }""");

        await _hub.SendSkipAsync();

        Assert.DoesNotContain(conn1.Sent, f => f.Contains("skip"));
        Assert.Contains("""{"v":1,"type":"skip","tabId":9}""", conn2.Sent);
    }

    [Fact]
    public async Task SendSkip_WithNoActiveAd_SendsNothing()
    {
        var conn = await ConnectAndAuthenticateAsync();

        await _hub.SendSkipAsync();

        Assert.Equal(["""{"v":1,"type":"helloAck"}"""], conn.Sent);
    }

    [Fact]
    public async Task ExtensionConnected_TracksFirstAuthAndLastDisconnect()
    {
        var seen = new List<bool>();
        _hub.ExtensionConnectedChanged += (_, connected) => seen.Add(connected);
        Assert.False(_hub.ExtensionConnected);

        await ConnectAndAuthenticateAsync("conn1");
        await ConnectAndAuthenticateAsync("conn2");

        Assert.True(_hub.ExtensionConnected);
        Assert.Equal([true], seen);

        _hub.OnDisconnected("conn1");
        Assert.True(_hub.ExtensionConnected);

        _hub.OnDisconnected("conn2");
        Assert.False(_hub.ExtensionConnected);
        Assert.Equal([true, false], seen);
    }

    [Fact]
    public async Task Heartbeat_PingsAuthenticatedConnectionsEachInterval()
    {
        var conn = await ConnectAndAuthenticateAsync();

        _time.Advance(BridgeHub.HeartbeatInterval);

        Assert.Contains("""{"v":1,"type":"ping"}""", conn.Sent);
    }

    [Fact]
    public async Task SilentConnection_IsDroppedAtIdleTimeout_AndItsAdCleared()
    {
        var conn = await ConnectAndAuthenticateAsync();
        await _hub.OnFrameAsync("conn1", """{ "v":1, "type":"adState", "tabId":7, "adActive":true }""");

        _time.Advance(BridgeHub.IdleTimeout);

        Assert.True(conn.Closed);
        Assert.Null(_tracker.ActiveAd);
    }

    [Fact]
    public async Task Pong_ResetsTheIdleClock()
    {
        var conn = await ConnectAndAuthenticateAsync();

        _time.Advance(BridgeHub.IdleTimeout - BridgeHub.HeartbeatInterval);
        await _hub.OnFrameAsync("conn1", """{ "v":1, "type":"pong" }""");
        _time.Advance(BridgeHub.IdleTimeout - BridgeHub.HeartbeatInterval);

        Assert.False(conn.Closed);
    }

    private sealed class FakeBridgeConnection(string id) : IBridgeConnection
    {
        public string Id { get; } = id;
        public List<string> Sent { get; } = [];
        public bool Closed { get; private set; }

        public Task SendAsync(string frame)
        {
            Sent.Add(frame);
            return Task.CompletedTask;
        }

        public Task CloseAsync()
        {
            Closed = true;
            return Task.CompletedTask;
        }
    }
}
