using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace GameDeck.Core.Bridge;

/// <summary>
/// The bridge's decision core: authenticates connections, feeds ad state to
/// the tracker, heartbeats, and routes skips. The transport
/// (<see cref="AdBridgeServer"/>) owns sockets and calls in; this class never
/// touches one directly, so it is fully testable with fakes.
/// </summary>
public sealed class BridgeHub : IDisposable
{
    public static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(15);
    public static readonly TimeSpan IdleTimeout = TimeSpan.FromSeconds(45);

    private readonly string _expectedToken;
    private readonly AdStateTracker _tracker;
    private readonly TimeProvider _time;
    private readonly ILogger _logger;
    private readonly ITimer _heartbeat;

    public BridgeHub(string expectedToken, AdStateTracker tracker, TimeProvider time, ILogger? logger = null)
    {
        _expectedToken = expectedToken;
        _tracker = tracker;
        _time = time;
        _logger = logger ?? NullLogger.Instance;
        _heartbeat = _time.CreateTimer(_ => OnHeartbeat(), null, HeartbeatInterval, HeartbeatInterval);
    }

    /// <summary>True while at least one authenticated extension socket is open.</summary>
    public bool ExtensionConnected { get; private set; }

    public event EventHandler<bool>? ExtensionConnectedChanged;

    private void UpdateExtensionConnected()
    {
        bool connected;
        lock (_sync)
        {
            connected = _connections.Values.Any(c => c.Authenticated);
            if (connected == ExtensionConnected)
                return;
            ExtensionConnected = connected;
        }
        ExtensionConnectedChanged?.Invoke(this, connected);
    }

    public void OnConnected(IBridgeConnection connection)
    {
        lock (_sync)
        {
            _connections[connection.Id] = new ConnectionState(connection)
            {
                LastActivity = _time.GetTimestamp(),
            };
        }
    }

    public async Task OnFrameAsync(string connectionId, string frame)
    {
        ConnectionState? state;
        lock (_sync)
        {
            if (!_connections.TryGetValue(connectionId, out state))
                return;
            state.LastActivity = _time.GetTimestamp();
        }

        var message = BridgeMessage.Parse(frame);
        if (!state.Authenticated)
        {
            if (message is HelloMessage hello && hello.Token == _expectedToken)
            {
                state.Authenticated = true;
                UpdateExtensionConnected();
                await state.Connection.SendAsync(BridgeMessage.Serialize(new HelloAckMessage()));
            }
            else
            {
                _logger.LogWarning("Bridge connection {Id} failed authentication; closing", connectionId);
                await DropAsync(connectionId);
            }
            return;
        }

        if (message is AdStateMessage ad)
            _tracker.OnAdState(connectionId, ad.TabId, ad.AdActive, ad.Skippable, ad.SecondsUntilSkippable);
    }

    /// <summary>Sends a skip for the currently active ad; no-op when there is none.</summary>
    public async Task SendSkipAsync()
    {
        if (_tracker.ActiveAd is not { } ad)
            return;

        ConnectionState? state;
        lock (_sync)
        {
            if (!_connections.TryGetValue(ad.ConnectionId, out state))
                return;
        }
        await state.Connection.SendAsync(BridgeMessage.Serialize(new SkipMessage(ad.TabId)));
    }

    private async Task DropAsync(string connectionId)
    {
        ConnectionState? state;
        lock (_sync)
        {
            if (!_connections.Remove(connectionId, out state))
                return;
        }
        _tracker.OnConnectionClosed(connectionId);
        UpdateExtensionConnected();
        await state.Connection.CloseAsync();
    }

    private void OnHeartbeat()
    {
        List<string> idle = [];
        List<IBridgeConnection> live = [];
        lock (_sync)
        {
            foreach (var (id, state) in _connections)
            {
                if (_time.GetElapsedTime(state.LastActivity) >= IdleTimeout)
                    idle.Add(id);
                else if (state.Authenticated)
                    live.Add(state.Connection);
            }
        }

        foreach (var id in idle)
        {
            _logger.LogWarning("Bridge connection {Id} idle past {Timeout}; dropping", id, IdleTimeout);
            _ = DropAsync(id);
        }
        foreach (var connection in live)
            _ = SendQuietlyAsync(connection, BridgeMessage.Serialize(new PingMessage()));
    }

    private async Task SendQuietlyAsync(IBridgeConnection connection, string frame)
    {
        try
        {
            await connection.SendAsync(frame);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Bridge send to {Id} failed", connection.Id);
        }
    }

    public void Dispose() => _heartbeat.Dispose();

    public void OnDisconnected(string connectionId)
    {
        lock (_sync)
        {
            _connections.Remove(connectionId);
        }
        _tracker.OnConnectionClosed(connectionId);
        UpdateExtensionConnected();
    }

    private readonly object _sync = new();
    private readonly Dictionary<string, ConnectionState> _connections = [];

    private sealed class ConnectionState(IBridgeConnection connection)
    {
        public IBridgeConnection Connection { get; } = connection;
        public bool Authenticated { get; set; }
        public long LastActivity { get; set; }
    }
}
