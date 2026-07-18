using GameDeck.Core.Bridge;

namespace GameDeck.Core.Tests;

public class AdStateTrackerTests
{
    private readonly AdStateTracker _tracker = new();
    private readonly List<AdStatus?> _changes = [];

    private void Record() => _tracker.ActiveAdChanged += (_, status) => _changes.Add(status);

    [Fact]
    public void AdAppears_BecomesActiveAdAndRaisesChange()
    {
        Record();

        _tracker.OnAdState("conn1", tabId: 123, adActive: true, skippable: false, secondsUntilSkippable: 4);

        Assert.Equal(new AdStatus("conn1", 123, Skippable: false, SecondsUntilSkippable: 4), _tracker.ActiveAd);
        Assert.Equal([_tracker.ActiveAd], _changes);
    }

    [Fact]
    public void ActiveAdEnds_FallsBackToAnotherStillActiveAd()
    {
        _tracker.OnAdState("conn1", tabId: 1, adActive: true, skippable: true, secondsUntilSkippable: null);
        _tracker.OnAdState("conn1", tabId: 2, adActive: true, skippable: false, secondsUntilSkippable: 5);
        Assert.Equal(2, _tracker.ActiveAd?.TabId); // most recent report wins

        _tracker.OnAdState("conn1", tabId: 2, adActive: false, skippable: false, secondsUntilSkippable: null);

        Assert.Equal(new AdStatus("conn1", 1, Skippable: true, SecondsUntilSkippable: null), _tracker.ActiveAd);
    }

    [Fact]
    public void UnchangedReport_RaisesNothing()
    {
        _tracker.OnAdState("conn1", tabId: 1, adActive: true, skippable: false, secondsUntilSkippable: 4);
        Record();

        _tracker.OnAdState("conn1", tabId: 1, adActive: true, skippable: false, secondsUntilSkippable: 4);
        _tracker.OnAdState("conn1", tabId: 9, adActive: false, skippable: false, secondsUntilSkippable: null);

        Assert.Empty(_changes);
    }

    [Fact]
    public void ConnectionClosed_ClearsItsAds_AndFallsBackToOtherConnections()
    {
        _tracker.OnAdState("conn1", tabId: 1, adActive: true, skippable: false, secondsUntilSkippable: null);
        _tracker.OnAdState("conn2", tabId: 7, adActive: true, skippable: true, secondsUntilSkippable: null);
        _tracker.OnAdState("conn2", tabId: 8, adActive: true, skippable: false, secondsUntilSkippable: 2);

        _tracker.OnConnectionClosed("conn2");

        Assert.Equal(new AdStatus("conn1", 1, Skippable: false, SecondsUntilSkippable: null), _tracker.ActiveAd);

        _tracker.OnConnectionClosed("conn1");

        Assert.Null(_tracker.ActiveAd);
    }

    [Fact]
    public void AdBecomesSkippable_RaisesUpdatedStatus()
    {
        _tracker.OnAdState("conn1", tabId: 1, adActive: true, skippable: false, secondsUntilSkippable: 4);
        Record();

        _tracker.OnAdState("conn1", tabId: 1, adActive: true, skippable: true, secondsUntilSkippable: null);

        Assert.Equal([new AdStatus("conn1", 1, Skippable: true, SecondsUntilSkippable: null)], _changes);
    }

    [Fact]
    public void LastAdEnds_RaisesNull()
    {
        _tracker.OnAdState("conn1", tabId: 1, adActive: true, skippable: true, secondsUntilSkippable: null);
        Record();

        _tracker.OnAdState("conn1", tabId: 1, adActive: false, skippable: false, secondsUntilSkippable: null);

        Assert.Equal([null], _changes);
        Assert.Null(_tracker.ActiveAd);
    }
}
