using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace GameDeck.Core.Bridge;

/// <summary>
/// Localhost WebSocket endpoint for the browser extension. Binds
/// http://127.0.0.1:{port}/bridge/ (loopback needs no URL ACL, verified
/// 2026-07-18) trying <see cref="BridgePorts.Candidates"/> in order. Owns
/// sockets only: every inbound frame is funneled through a single-consumer
/// channel into <see cref="BridgeHub"/>, which makes all decisions.
/// </summary>
public sealed class AdBridgeServer : IDisposable
{
    private readonly BridgeHub _hub;
    private readonly ILogger _logger;
    private readonly string _dataDirectory;
    private readonly CancellationTokenSource _cts = new();
    private readonly Channel<(string ConnectionId, string Frame)> _inbound =
        Channel.CreateUnbounded<(string, string)>(new UnboundedChannelOptions { SingleReader = true });

    private HttpListener? _listener;

    public AdBridgeServer(string expectedToken, AdStateTracker tracker, TimeProvider time,
        ILogger? logger = null, string? dataDirectory = null)
    {
        _logger = logger ?? NullLogger.Instance;
        _hub = new BridgeHub(expectedToken, tracker, time, logger);
        _dataDirectory = dataDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GameDeck");
    }

    /// <summary>The bound port, or null when the server is not running.</summary>
    public int? Port { get; private set; }

    /// <summary>True while an authenticated extension is connected.</summary>
    public bool ExtensionConnected => _hub.ExtensionConnected;

    /// <summary>Raised from worker threads; marshal before touching UI.</summary>
    public event EventHandler<bool>? ExtensionConnectedChanged
    {
        add => _hub.ExtensionConnectedChanged += value;
        remove => _hub.ExtensionConnectedChanged -= value;
    }

    /// <summary>Sends a skip for the currently active ad; no-op when there is none.</summary>
    public Task SendSkipAsync() => _hub.SendSkipAsync();

    /// <summary>Binds a port and starts serving. False when every candidate port is taken.</summary>
    public bool Start()
    {
        Port = BridgePorts.Select(TryBind);
        if (Port is null)
        {
            _logger.LogWarning("Bridge disabled: all candidate ports are in use ({Ports})",
                string.Join(", ", BridgePorts.Candidates));
            return false;
        }

        WritePortFile(Port.Value);
        _ = AcceptLoopAsync(_listener!, _cts.Token);
        _ = ConsumeLoopAsync(_cts.Token);
        _logger.LogInformation("Bridge listening on 127.0.0.1:{Port}", Port);
        return true;
    }

    private bool TryBind(int port)
    {
        var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/bridge/");
        try
        {
            listener.Start();
            _listener = listener;
            return true;
        }
        catch (HttpListenerException)
        {
            listener.Close();
            return false;
        }
    }

    private void WritePortFile(int port)
    {
        try
        {
            Directory.CreateDirectory(_dataDirectory);
            File.WriteAllText(Path.Combine(_dataDirectory, "bridge.json"), $$"""{ "port": {{port}} }""");
        }
        catch (Exception ex)
        {
            // Diagnostic aid only; the extension probes ports on its own.
            _logger.LogWarning(ex, "Could not write bridge.json");
        }
    }

    private async Task AcceptLoopAsync(HttpListener listener, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await listener.GetContextAsync();
            }
            catch (Exception) when (ct.IsCancellationRequested || !listener.IsListening)
            {
                return;
            }

            if (!context.Request.IsWebSocketRequest)
            {
                context.Response.StatusCode = 400;
                context.Response.Close();
                continue;
            }

            // Defense in depth on top of the token: reject browser-page
            // origins so a malicious site cannot even reach the handshake.
            if (!BridgeOrigin.IsAllowed(context.Request.Headers["Origin"]))
            {
                _logger.LogWarning("Bridge rejected connection from disallowed origin {Origin}",
                    context.Request.Headers["Origin"]);
                context.Response.StatusCode = 403;
                context.Response.Close();
                continue;
            }

            _ = ServeSocketAsync(context, ct);
        }
    }

    private async Task ServeSocketAsync(HttpListenerContext context, CancellationToken ct)
    {
        var connectionId = Guid.NewGuid().ToString("N");
        WebSocket socket;
        try
        {
            socket = (await context.AcceptWebSocketAsync(subProtocol: null)).WebSocket;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "WebSocket handshake failed");
            return;
        }

        var connection = new SocketConnection(connectionId, socket);
        _hub.OnConnected(connection);
        _logger.LogDebug("Bridge connection {Id} opened", connectionId);

        var buffer = new byte[16 * 1024];
        var message = new MemoryStream();
        try
        {
            while (socket.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                var result = await socket.ReceiveAsync(buffer, ct);
                if (result.MessageType == WebSocketMessageType.Close)
                    break;
                message.Write(buffer, 0, result.Count);
                if (!result.EndOfMessage)
                    continue;

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var frame = Encoding.UTF8.GetString(message.GetBuffer(), 0, (int)message.Length);
                    await _inbound.Writer.WriteAsync((connectionId, frame), ct);
                }
                message.SetLength(0);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Bridge connection {Id} receive failed", connectionId);
        }
        finally
        {
            _hub.OnDisconnected(connectionId);
            connection.Dispose();
            _logger.LogDebug("Bridge connection {Id} closed", connectionId);
        }
    }

    private async Task ConsumeLoopAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var (connectionId, frame) in _inbound.Reader.ReadAllAsync(ct))
                await _hub.OnFrameAsync(connectionId, frame);
        }
        catch (OperationCanceledException)
        {
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _listener?.Close();
        _inbound.Writer.TryComplete();
        _hub.Dispose();
        _cts.Dispose();
    }

    /// <summary>Serializes sends; closing is abortive because the peer is a browser that reconnects anyway.</summary>
    private sealed class SocketConnection(string id, WebSocket socket) : IBridgeConnection, IDisposable
    {
        private readonly SemaphoreSlim _sendLock = new(1, 1);

        public string Id { get; } = id;

        public async Task SendAsync(string frame)
        {
            await _sendLock.WaitAsync();
            try
            {
                if (socket.State == WebSocketState.Open)
                    await socket.SendAsync(Encoding.UTF8.GetBytes(frame), WebSocketMessageType.Text,
                        endOfMessage: true, CancellationToken.None);
            }
            finally
            {
                _sendLock.Release();
            }
        }

        public Task CloseAsync()
        {
            socket.Abort();
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            socket.Dispose();
            _sendLock.Dispose();
        }
    }
}
