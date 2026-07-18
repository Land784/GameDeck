namespace GameDeck.Core.Bridge;

/// <summary>
/// The bridge's fixed candidate ports. The extension tries the same list in
/// the same order, so both sides converge without any out-of-band discovery.
/// </summary>
public static class BridgePorts
{
    public static IReadOnlyList<int> Candidates { get; } = [52780, 52781, 52782, 52783, 52784];

    public static int? Select(Func<int, bool> tryBind)
    {
        foreach (var port in Candidates)
        {
            if (tryBind(port))
                return port;
        }
        return null;
    }
}
