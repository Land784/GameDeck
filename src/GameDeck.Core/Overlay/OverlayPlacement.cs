namespace GameDeck.Core.Overlay;

public enum OverlayCorner
{
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,
}

/// <summary>A rectangle in device-independent units. Core stays WPF-free.</summary>
public sealed record RectD(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;
    public double CenterX => X + Width / 2;
    public double CenterY => Y + Height / 2;
}

/// <summary>
/// Overlay position math: a placement is a corner anchor plus inward offsets,
/// which survives resolution and work-area changes better than absolute
/// coordinates. Pure; the window applies the results.
/// </summary>
public static class OverlayPlacement
{
    public const double DefaultMargin = 16;
    public const double SnapDistance = 32;

    public static (double Left, double Top) Position(
        RectD workArea, double width, double height, OverlayCorner corner, double offsetX, double offsetY)
    {
        var left = corner is OverlayCorner.TopLeft or OverlayCorner.BottomLeft
            ? workArea.X + offsetX
            : workArea.Right - width - offsetX;
        var top = corner is OverlayCorner.TopLeft or OverlayCorner.TopRight
            ? workArea.Y + offsetY
            : workArea.Bottom - height - offsetY;
        return (left, top);
    }

    public static (OverlayCorner Corner, double OffsetX, double OffsetY) FromWindow(RectD workArea, RectD window)
    {
        var left = window.CenterX < workArea.CenterX;
        var top = window.CenterY < workArea.CenterY;
        var corner = (left, top) switch
        {
            (true, true) => OverlayCorner.TopLeft,
            (false, true) => OverlayCorner.TopRight,
            (true, false) => OverlayCorner.BottomLeft,
            (false, false) => OverlayCorner.BottomRight,
        };

        var offsetX = left ? window.X - workArea.X : workArea.Right - window.Right;
        var offsetY = top ? window.Y - workArea.Y : workArea.Bottom - window.Bottom;

        return (corner, Adjust(offsetX, workArea.Width - window.Width),
            Adjust(offsetY, workArea.Height - window.Height));
    }

    private static double Adjust(double offset, double max)
    {
        if (Math.Abs(offset - DefaultMargin) <= SnapDistance)
            return DefaultMargin;
        return Math.Clamp(offset, 0, Math.Max(0, max));
    }
}
