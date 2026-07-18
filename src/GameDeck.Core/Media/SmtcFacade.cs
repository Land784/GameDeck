using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace GameDeck.Core.Media;

/// <summary>Real WinRT-backed facade. Only touched by the app, never by tests.</summary>
public sealed class SmtcFacade : ISmtcFacade
{
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private readonly object _gate = new();
    private Dictionary<GlobalSystemMediaTransportControlsSession, SmtcSessionWrapper> _wrappers = new();

    public event EventHandler? CurrentSessionChanged;
    public event EventHandler? SessionsChanged;

    public async Task InitializeAsync()
    {
        _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        _manager.CurrentSessionChanged += (_, _) => CurrentSessionChanged?.Invoke(this, EventArgs.Empty);
        _manager.SessionsChanged += (_, _) => SessionsChanged?.Invoke(this, EventArgs.Empty);
    }

    public ISmtcSession? CurrentSession
    {
        get
        {
            var raw = _manager?.GetCurrentSession();
            return raw is null ? null : Wrap(raw);
        }
    }

    public IReadOnlyList<ISmtcSession> Sessions =>
        _manager is null
            ? Array.Empty<ISmtcSession>()
            : _manager.GetSessions().Select(Wrap).ToList();

    /// <summary>
    /// One stable wrapper per underlying session so event subscriptions on a
    /// wrapper survive repeated Sessions/CurrentSession reads.
    /// </summary>
    private SmtcSessionWrapper Wrap(GlobalSystemMediaTransportControlsSession raw)
    {
        lock (_gate)
        {
            if (!_wrappers.TryGetValue(raw, out var wrapper))
            {
                wrapper = new SmtcSessionWrapper(raw);
                _wrappers[raw] = wrapper;
            }

            return wrapper;
        }
    }

    public ValueTask DisposeAsync()
    {
        lock (_gate) _wrappers = new();
        _manager = null;
        return ValueTask.CompletedTask;
    }
}

internal sealed class SmtcSessionWrapper : ISmtcSession
{
    private readonly GlobalSystemMediaTransportControlsSession _raw;

    public SmtcSessionWrapper(GlobalSystemMediaTransportControlsSession raw)
    {
        _raw = raw;
        _raw.MediaPropertiesChanged += (_, _) => MediaPropertiesChanged?.Invoke(this, EventArgs.Empty);
        _raw.PlaybackInfoChanged += (_, _) => PlaybackInfoChanged?.Invoke(this, EventArgs.Empty);
        _raw.TimelinePropertiesChanged += (_, _) => TimelinePropertiesChanged?.Invoke(this, EventArgs.Empty);
    }

    public string AppId => _raw.SourceAppUserModelId;

    public event EventHandler? MediaPropertiesChanged;
    public event EventHandler? PlaybackInfoChanged;
    public event EventHandler? TimelinePropertiesChanged;

    public async Task<SmtcMediaProperties?> TryGetMediaPropertiesAsync()
    {
        var props = await _raw.TryGetMediaPropertiesAsync();
        if (props is null) return null;

        byte[]? art = null;
        if (props.Thumbnail is not null)
        {
            try
            {
                art = await ReadAllBytesAsync(props.Thumbnail);
            }
            catch
            {
                // Art is best-effort; metadata without art beats no metadata.
            }
        }

        return new SmtcMediaProperties(props.Title, props.Artist, props.AlbumTitle, art);
    }

    public PlaybackState GetPlaybackState() =>
        _raw.GetPlaybackInfo()?.PlaybackStatus switch
        {
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Closed => PlaybackState.Closed,
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Opened => PlaybackState.Opened,
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Changing => PlaybackState.Changing,
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Stopped => PlaybackState.Stopped,
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing => PlaybackState.Playing,
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused => PlaybackState.Paused,
            _ => PlaybackState.Unknown,
        };

    public (TimeSpan Position, TimeSpan Duration) GetTimeline()
    {
        var t = _raw.GetTimelineProperties();
        if (t is null) return (TimeSpan.Zero, TimeSpan.Zero);
        return (t.Position, t.EndTime - t.StartTime);
    }

    public async Task<bool> TryPlayPauseAsync() => await _raw.TryTogglePlayPauseAsync();
    public async Task<bool> TrySkipNextAsync() => await _raw.TrySkipNextAsync();
    public async Task<bool> TrySkipPreviousAsync() => await _raw.TrySkipPreviousAsync();

    private static async Task<byte[]> ReadAllBytesAsync(IRandomAccessStreamReference reference)
    {
        using var stream = await reference.OpenReadAsync();
        var bytes = new byte[stream.Size];
        var buffer = await stream.ReadAsync(bytes.AsBuffer(), (uint)stream.Size, InputStreamOptions.None);
        buffer.CopyTo(bytes);
        return bytes;
    }
}
