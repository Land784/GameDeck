using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace GameDeck.App.Diagnostics;

/// <summary>
/// Last-resort modal shown when an unhandled exception tears the app down.
/// Built entirely in code (no XAML, no pack resources) so it does not depend
/// on the resource-loading path, which may itself be part of what failed.
/// </summary>
internal static class CrashDialog
{
    public static void Show(string logDir)
    {
        var message = new TextBlock
        {
            Text = "GameDeck hit a problem and closed.",
            FontSize = 14,
            TextWrapping = TextWrapping.Wrap,
        };

        var openButton = new Button
        {
            Content = "Open logs folder",
            MinWidth = 120,
            Padding = new Thickness(14, 6, 14, 6),
            IsDefault = true,
        };
        var closeButton = new Button
        {
            Content = "Close",
            MinWidth = 80,
            Margin = new Thickness(8, 0, 0, 0),
            Padding = new Thickness(14, 6, 14, 6),
            IsCancel = true,
        };

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 18, 0, 0),
        };
        buttons.Children.Add(openButton);
        buttons.Children.Add(closeButton);

        var root = new StackPanel { Margin = new Thickness(20) };
        root.Children.Add(message);
        root.Children.Add(buttons);

        var window = new Window
        {
            Title = "GameDeck",
            Content = root,
            Width = 360,
            SizeToContent = SizeToContent.Height,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            ShowInTaskbar = true,
        };

        openButton.Click += (_, _) =>
        {
            OpenLogsFolder(logDir);
            window.Close();
        };
        closeButton.Click += (_, _) => window.Close();

        window.ShowDialog();
    }

    private static void OpenLogsFolder(string logDir)
    {
        try
        {
            Directory.CreateDirectory(logDir);
            Process.Start(new ProcessStartInfo
            {
                FileName = logDir,
                UseShellExecute = true,
            });
        }
        catch
        {
            // If Explorer will not even open, there is nothing more this dialog can do.
        }
    }
}
