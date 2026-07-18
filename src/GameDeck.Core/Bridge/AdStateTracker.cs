namespace GameDeck.Core.Bridge;

/// <summary>The ad the overlay should surface right now, and where to route a skip.</summary>
public sealed record AdStatus(string ConnectionId, int TabId, bool Skippable, int? SecondsUntilSkippable);

/// <summary>
/// Tracks ad state per connection and tab, and elects the single "active ad"
/// the rest of the app cares about. Pure logic; the transport feeds it.
/// </summary>
public sealed class AdStateTracker
{
    private readonly Dictionary<(string ConnectionId, int TabId), (AdStatus Status, long Seq)> _ads = [];
    private long _seq;

    /// <summary>The most recently reported active ad, or null when none is playing.</summary>
    public AdStatus? ActiveAd { get; private set; }

    /// <summary>Raised when <see cref="ActiveAd"/> changes; null means no ad anywhere.</summary>
    public event EventHandler<AdStatus?>? ActiveAdChanged;

    public void OnAdState(string connectionId, int tabId, bool adActive, bool skippable, int? secondsUntilSkippable)
    {
        var key = (connectionId, tabId);
        if (adActive)
            _ads[key] = (new AdStatus(connectionId, tabId, skippable, secondsUntilSkippable), ++_seq);
        else
            _ads.Remove(key);
        ReelectActiveAd();
    }

    public void OnConnectionClosed(string connectionId)
    {
        var stale = _ads.Keys.Where(key => key.ConnectionId == connectionId).ToList();
        foreach (var key in stale)
            _ads.Remove(key);
        ReelectActiveAd();
    }

    private void ReelectActiveAd()
    {
        var elected = _ads.Count == 0
            ? null
            : _ads.Values.MaxBy(entry => entry.Seq).Status;
        if (elected == ActiveAd)
            return;
        ActiveAd = elected;
        ActiveAdChanged?.Invoke(this, elected);
    }
}
