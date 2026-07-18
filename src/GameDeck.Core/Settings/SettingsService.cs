using System.Text.Json;
using System.Text.Json.Serialization;

namespace GameDeck.Core.Settings;

/// <summary>
/// JSON settings at %APPDATA%\GameDeck\settings.json. Loads defaults when the
/// file is missing or unreadable; saves atomically (temp file + replace).
/// </summary>
public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _filePath;

    public SettingsService(string? directory = null)
    {
        directory ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GameDeck");
        _filePath = Path.Combine(directory, "settings.json");
    }

    public AppSettings Current { get; private set; } = new();

    public AppSettings Load()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var loaded = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_filePath), JsonOptions);
                if (loaded is not null)
                {
                    Current = loaded;
                    return Current;
                }
            }
        }
        catch
        {
            // Corrupt or unreadable settings must never block startup.
        }

        Current = new AppSettings();
        return Current;
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        var tmp = _filePath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(Current, JsonOptions));
        if (File.Exists(_filePath))
            File.Replace(tmp, _filePath, destinationBackupFileName: null);
        else
            File.Move(tmp, _filePath);
    }
}
