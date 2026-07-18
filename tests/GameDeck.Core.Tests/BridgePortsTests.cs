using GameDeck.Core.Bridge;

namespace GameDeck.Core.Tests;

public class BridgePortsTests
{
    [Fact]
    public void SelectsFirstBindablePortInCandidateOrder()
    {
        var tried = new List<int>();

        var port = BridgePorts.Select(p =>
        {
            tried.Add(p);
            return p == 52782;
        });

        Assert.Equal(52782, port);
        Assert.Equal([52780, 52781, 52782], tried);
    }

    [Fact]
    public void AllCandidatePortsTaken_YieldsNull()
    {
        Assert.Null(BridgePorts.Select(_ => false));
    }
}
