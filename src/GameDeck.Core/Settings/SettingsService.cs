using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace GameDeck.Core.Settings;

/// <summary>
/// JSON settings at %APPDATA%\GameDeck\settings.json. Loads defaults when the
/// file is missing or unreadable; saves atomically (temp file + replace).
/// All writes go through <see cref="Update"/> so mutate-and-persist is one
/// operation and concurrent writers can't lose updates.
/// </summary>
public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _filePath;
    private readonly ILogger _logger;
    private readonly object _gate = new();

    public SettingsService(string? directory = null, ILogger? logger = null)
    {
        directory ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GameDeck");
        _filePath = Path.Combine(directory, "settings.json");
        _logger = logger ?? NullLogger.Instance;
    }

    public AppSettings Current { get; private set; } = new();

    public AppSettings Load()
    {
        lock (_gate)
        {
            try
            {
                if (File.Exists(_filePath))
                {
                    var loaded = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_filePath), JsonOptions);
                    if (loaded is not null)
                    {
                        Current = loaded;
                        EnsureGeneratedFields();
                        return Current;
                    }
                }
            }
            catch (Exception ex)
            {
                // Corrupt or unreadable settings must never block startup.
                _logger.LogWarning(ex, "Settings unreadable at {Path}; using defaults", _filePath);
            }

            Current = new AppSettings();
            EnsureGeneratedFields();
            return Current;
        }
    }

    /// <summary>Post-load fixups that must persist. Called inside the gate.</summary>
    private void EnsureGeneratedFields()
    {
        if (Current.BridgeToken is null)
        {
            // Cryptographically random 128-bit token (guaranteed CSPRNG,
            // unlike Guid.NewGuid). It gates every bridge action.
            Current.BridgeToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
            Save();
        }
    }

    /// <summary>Mutates settings and persists in one atomic step.</summary>
    public void Update(Action<AppSettings> mutate)
    {
        lock (_gate)
        {
            mutate(Current);
            Save();
        }
    }

    private void Save()
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
