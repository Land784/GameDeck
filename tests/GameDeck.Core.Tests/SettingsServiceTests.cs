using GameDeck.Core.Hotkeys;
using GameDeck.Core.Settings;

namespace GameDeck.Core.Tests;

public class SettingsServiceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "GameDeckTests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    [Fact]
    public void Load_MissingFile_ReturnsDefaults()
    {
        var service = new SettingsService(_dir);

        var settings = service.Load();

        Assert.Equal(1, settings.Version);
        Assert.Null(settings.PreferredAppId);
        Assert.Equal(HotkeyBinding.Defaults.Count, settings.Hotkeys.Count);
    }

    [Fact]
    public void SaveThenLoad_RoundTrips()
    {
        var service = new SettingsService(_dir);
        service.Load();
        service.Update(s =>
        {
            s.PreferredAppId = "Spotify.exe";
            s.Hotkeys[0] = s.Hotkeys[0] with { VirtualKey = 0x42 };
        });

        var reloaded = new SettingsService(_dir).Load();

        Assert.Equal("Spotify.exe", reloaded.PreferredAppId);
        Assert.Equal(0x42u, reloaded.Hotkeys[0].VirtualKey);
    }

    [Fact]
    public void Load_CorruptJson_FallsBackToDefaults()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "settings.json"), "{ not valid json !!");

        var settings = new SettingsService(_dir).Load();

        Assert.Equal(1, settings.Version);
    }

    [Fact]
    public void Save_OverExistingFile_Replaces()
    {
        var service = new SettingsService(_dir);
        service.Load();
        service.Update(_ => { });
        service.Update(s => s.PreferredAppId = "MSEdge");

        Assert.Equal("MSEdge", new SettingsService(_dir).Load().PreferredAppId);
        Assert.False(File.Exists(Path.Combine(_dir, "settings.json.tmp")));
    }
}
