using GameDeck.Core.Overlay;

namespace GameDeck.Core.Tests;

public class OverlayPlacementTests
{
    private static readonly RectD Work = new(0, 0, 1920, 1040);
    private const double W = 320;
    private const double H = 120;

    [Theory]
    [InlineData(OverlayCorner.TopLeft, 16, 16, 16, 16)]
    [InlineData(OverlayCorner.TopRight, 16, 16, 1920 - 320 - 16, 16)]
    [InlineData(OverlayCorner.BottomLeft, 16, 16, 16, 1040 - 120 - 16)]
    [InlineData(OverlayCorner.BottomRight, 16, 16, 1920 - 320 - 16, 1040 - 120 - 16)]
    public void Position_AnchorsToEachCornerWithInwardOffsets(
        OverlayCorner corner, double ox, double oy, double expectedLeft, double expectedTop)
    {
        var (left, top) = OverlayPlacement.Position(Work, W, H, corner, ox, oy);

        Assert.Equal(expectedLeft, left);
        Assert.Equal(expectedTop, top);
    }

    [Fact]
    public void Position_HonorsWorkAreaOrigin_OnSecondaryMonitors()
    {
        var secondary = new RectD(1920, 200, 2560, 1400);

        var (left, top) = OverlayPlacement.Position(secondary, W, H, OverlayCorner.TopRight, 16, 16);

        Assert.Equal(1920 + 2560 - W - 16, left);
        Assert.Equal(200 + 16, top);
    }

    [Fact]
    public void FromWindow_PicksTheNearestCornerByWindowCenter()
    {
        var window = new RectD(1500, 700, W, H); // bottom-right quadrant

        var placement = OverlayPlacement.FromWindow(Work, window);

        Assert.Equal(OverlayCorner.BottomRight, placement.Corner);
    }

    [Fact]
    public void FromWindow_SnapsOffsetsNearTheDefaultMargin()
    {
        // 30 px from the right edge, 25 from the top: both within snap range.
        var window = new RectD(1920 - W - 30, 25, W, H);

        var placement = OverlayPlacement.FromWindow(Work, window);

        Assert.Equal(OverlayCorner.TopRight, placement.Corner);
        Assert.Equal(OverlayPlacement.DefaultMargin, placement.OffsetX);
        Assert.Equal(OverlayPlacement.DefaultMargin, placement.OffsetY);
    }

    [Fact]
    public void FromWindow_KeepsFreeOffsetsAwayFromTheMargin()
    {
        var window = new RectD(1920 - W - 300, 250, W, H);

        var placement = OverlayPlacement.FromWindow(Work, window);

        Assert.Equal(OverlayCorner.TopRight, placement.Corner);
        Assert.Equal(300, placement.OffsetX);
        Assert.Equal(250, placement.OffsetY);
    }

    [Fact]
    public void FromWindow_ClampsWindowsDraggedOutsideTheWorkArea()
    {
        var window = new RectD(-100, -50, W, H);

        var placement = OverlayPlacement.FromWindow(Work, window);

        Assert.Equal(OverlayCorner.TopLeft, placement.Corner);
        Assert.Equal(0, placement.OffsetX);
        Assert.Equal(0, placement.OffsetY);
    }
}
