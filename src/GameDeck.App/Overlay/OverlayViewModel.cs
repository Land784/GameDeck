using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GameDeck.Core.Media;

namespace GameDeck.App.Overlay;

/// <summary>Bindable card state. Only touched on the UI thread.</summary>
public sealed class OverlayViewModel : INotifyPropertyChanged
{
    private string _title = "Nothing playing";
    private string _artist = string.Empty;
    private ImageSource? _artImage;
    private double _progress;
    private bool _isInteractive;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Title
    {
        get => _title;
        private set => Set(ref _title, value, nameof(Title));
    }

    public string Artist
    {
        get => _artist;
        private set => Set(ref _artist, value, nameof(Artist));
    }

    public ImageSource? ArtImage
    {
        get => _artImage;
        private set
        {
            _artImage = value;
            Raise(nameof(ArtImage));
            Raise(nameof(ArtVisibility));
            Raise(nameof(FallbackVisibility));
        }
    }

    public Visibility ArtVisibility => _artImage is null ? Visibility.Collapsed : Visibility.Visible;
    public Visibility FallbackVisibility => _artImage is null ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>0..1 for the bottom progress bar.</summary>
    public double Progress
    {
        get => _progress;
        set => Set(ref _progress, value, nameof(Progress));
    }

    public bool IsInteractive
    {
        get => _isInteractive;
        set => Set(ref _isInteractive, value, nameof(IsInteractive));
    }

    public void UpdateFromSnapshot(MediaSnapshot? snapshot)
    {
        Title = string.IsNullOrEmpty(snapshot?.Title) ? "Nothing playing" : snapshot!.Title;
        Artist = snapshot?.Artist ?? string.Empty;
        ArtImage = ToFrozenImage(snapshot?.AlbumArtPng);
        if (snapshot is null) Progress = 0;
    }

    private static ImageSource? ToFrozenImage(byte[]? png)
    {
        if (png is null || png.Length == 0) return null;
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = new MemoryStream(png);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null; // Corrupt art is cosmetic; the ♪ fallback shows instead.
        }
    }

    private void Set<T>(ref T field, T value, string name)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        Raise(name);
    }

    private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
