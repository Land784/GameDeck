using GameDeck.Core.Bridge;

namespace GameDeck.Core.Tests;

public class BridgeProtocolTests
{
    [Fact]
    public void Parse_Hello_YieldsClientVersionAndToken()
    {
        var msg = BridgeMessage.Parse(
            """{ "v":1, "type":"hello", "client":"extension", "ext":"1.0.0", "token":"abc-123" }""");

        var hello = Assert.IsType<HelloMessage>(msg);
        Assert.Equal("extension", hello.Client);
        Assert.Equal("1.0.0", hello.Ext);
        Assert.Equal("abc-123", hello.Token);
    }

    [Fact]
    public void Parse_ActiveAdState_YieldsTabAndSkippability()
    {
        var msg = BridgeMessage.Parse(
            """{ "v":1, "type":"adState", "tabId":123, "adActive":true, "skippable":false, "secondsUntilSkippable":4 }""");

        var ad = Assert.IsType<AdStateMessage>(msg);
        Assert.Equal(123, ad.TabId);
        Assert.True(ad.AdActive);
        Assert.False(ad.Skippable);
        Assert.Equal(4, ad.SecondsUntilSkippable);
    }

    [Fact]
    public void Parse_InactiveAdState_DefaultsOmittedSkipFields()
    {
        var msg = BridgeMessage.Parse("""{ "v":1, "type":"adState", "tabId":123, "adActive":false }""");

        var ad = Assert.IsType<AdStateMessage>(msg);
        Assert.False(ad.AdActive);
        Assert.False(ad.Skippable);
        Assert.Null(ad.SecondsUntilSkippable);
    }

    [Fact]
    public void Parse_Pong_YieldsPong()
    {
        Assert.IsType<PongMessage>(BridgeMessage.Parse("""{ "v":1, "type":"pong" }"""));
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("")]
    [InlineData("42")]
    [InlineData("[1,2,3]")]
    [InlineData("""{ "v":1, "type":"launchMissiles" }""")]
    [InlineData("""{ "v":1 }""")]
    [InlineData("""{ "v":2, "type":"pong" }""")]
    [InlineData("""{ "type":"pong" }""")]
    [InlineData("""{ "v":1, "type":"adState", "tabId":"oops", "adActive":true }""")]
    public void Parse_BadUnknownOrWrongVersionInput_YieldsNullWithoutThrowing(string json)
    {
        Assert.Null(BridgeMessage.Parse(json));
    }

    [Fact]
    public void Serialize_OutboundMessages_ProduceVersionedFramesTheExtensionUnderstands()
    {
        Assert.Equal("""{"v":1,"type":"helloAck"}""", BridgeMessage.Serialize(new HelloAckMessage()));
        Assert.Equal("""{"v":1,"type":"ping"}""", BridgeMessage.Serialize(new PingMessage()));
        Assert.Equal("""{"v":1,"type":"skip","tabId":123}""", BridgeMessage.Serialize(new SkipMessage(123)));
    }
}
