using System.Text.Json;

namespace GameDeck.Core.Bridge;

/// <summary>
/// Bridge protocol v1: JSON text frames exchanged with the browser extension
/// over the localhost WebSocket. See docs/bridge-protocol.md.
/// </summary>
public abstract record BridgeMessage
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    /// <summary>Parses one inbound frame. Malformed or unknown input yields null, never throws.</summary>
    public static BridgeMessage? Parse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("v", out var version)
                || !version.TryGetInt32(out var v) || v != 1
                || !root.TryGetProperty("type", out var type)
                || type.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            return type.GetString() switch
            {
                "hello" => root.Deserialize<HelloMessage>(Options),
                "adState" => root.Deserialize<AdStateMessage>(Options),
                "pong" => new PongMessage(),
                _ => null,
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Serializes an app-to-extension frame.</summary>
    public static string Serialize(BridgeMessage message) => message switch
    {
        HelloAckMessage => """{"v":1,"type":"helloAck"}""",
        PingMessage => """{"v":1,"type":"ping"}""",
        SkipMessage skip => $$"""{"v":1,"type":"skip","tabId":{{skip.TabId}}}""",
        _ => throw new NotSupportedException($"{message.GetType().Name} is not an app-to-extension message."),
    };
}

public sealed record HelloMessage(string Client, string Ext, string Token) : BridgeMessage;

public sealed record AdStateMessage(
    int TabId, bool AdActive, bool Skippable = false, int? SecondsUntilSkippable = null) : BridgeMessage;

public sealed record PongMessage : BridgeMessage;

public sealed record HelloAckMessage : BridgeMessage;

public sealed record PingMessage : BridgeMessage;

public sealed record SkipMessage(int TabId) : BridgeMessage;
