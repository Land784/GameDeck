using System.Windows;
using H.NotifyIcon;

namespace GameDeck.App.Tray;

/// <summary>Tray icon from the bundled multi-size app.ico.</summary>
public static class TrayIconFactory
{
    public static void ApplyGeneratedIcon(TaskbarIcon icon)
    {
        var resource = Application.GetResourceStream(
            new Uri("pack://application:,,,/Assets/app.ico"));
        if (resource is null) return;
        using var stream = resource.Stream;
        icon.Icon = new System.Drawing.Icon(stream, new System.Drawing.Size(16, 16));
    }
}
