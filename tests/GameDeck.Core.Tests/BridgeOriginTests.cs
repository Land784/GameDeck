using GameDeck.Core.Bridge;
using Xunit;

namespace GameDeck.Core.Tests;

public class BridgeOriginTests
{
    [Theory]
    [InlineData(null)]                              // non-browser client (no Origin)
    [InlineData("")]                                // no Origin
    [InlineData("chrome-extension://abcdefghijklmnop")]
    [InlineData("CHROME-EXTENSION://ABC")]          // case-insensitive
    [InlineData("moz-extension://abc")]             // future Firefox port
    public void Allows_extensions_and_nonbrowser_clients(string? origin)
    {
        Assert.True(BridgeOrigin.IsAllowed(origin));
    }

    [Theory]
    [InlineData("https://evil.example.com")]
    [InlineData("http://localhost:3000")]
    [InlineData("http://127.0.0.1:52780")]
    [InlineData("https://www.youtube.com")]
    [InlineData("file://")]
    [InlineData("ws://127.0.0.1:52780")]
    public void Rejects_web_page_origins(string origin)
    {
        Assert.False(BridgeOrigin.IsAllowed(origin));
    }
}
