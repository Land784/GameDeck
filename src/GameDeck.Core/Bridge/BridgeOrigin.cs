namespace GameDeck.Core.Bridge;

/// <summary>
/// Decides which WebSocket <c>Origin</c> headers the bridge will accept, as
/// defense in depth on top of the token. A web page open in the user's
/// browser can reach a loopback WebSocket, so we reject browser-page origins
/// outright; only extension origins (and non-browser clients, which send no
/// Origin) are allowed to reach the auth step, where the token still gates
/// everything.
/// </summary>
public static class BridgeOrigin
{
    public static bool IsAllowed(string? origin)
    {
        // Non-browser clients (native tooling, tests) send no Origin. The
        // token still authenticates them, so let them reach the handshake.
        if (string.IsNullOrEmpty(origin)) return true;

        // Browser extensions are the only legitimate browser callers.
        return origin.StartsWith("chrome-extension://", StringComparison.OrdinalIgnoreCase)
            || origin.StartsWith("moz-extension://", StringComparison.OrdinalIgnoreCase);
        // Everything else (http, https, ws, wss, file, ...) is a web page and is rejected.
    }
}
