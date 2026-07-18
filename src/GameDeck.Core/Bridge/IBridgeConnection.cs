namespace GameDeck.Core.Bridge;

/// <summary>One live extension socket, as the hub sees it.</summary>
public interface IBridgeConnection
{
    string Id { get; }

    Task SendAsync(string frame);

    Task CloseAsync();
}
