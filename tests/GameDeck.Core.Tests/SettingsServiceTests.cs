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
        Assert.False(settings.StartWithWindows);
        Assert.Equal(HotkeyBinding.Defaults.Count, settings.Hotkeys.Count);
    }

    [Fact]
    public void SaveThenLoad_RoundTrips()
    {
        var service = new SettingsService(_dir);
        service.Load();
        service.Current.StartWithWindows = true;
        service.Current.PreferredAppId = "Spotify.exe";
        service.Current.Hotkeys[0] = service.Current.Hotkeys[0] with { VirtualKey = 0x42 };
        service.Save();

        var reloaded = new SettingsService(_dir).Load();

        Assert.True(reloaded.StartWithWindows);
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
        service.Save();
        service.Current.StartWithWindows = true;
        service.Save();

        Assert.True(new SettingsService(_dir).Load().StartWithWindows);
        Assert.False(File.Exists(Path.Combine(_dir, "settings.json.tmp")));
    }
}
