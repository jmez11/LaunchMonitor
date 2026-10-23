using LaunchMonitor.Core;
using Xunit;

namespace LaunchMonitor.Core.Tests;

public sealed class WindowPlacementTests
{
    [Fact]
    public void WindowMovesToNegativeCoordinateMonitorWithoutLosingItsRelativePosition()
    {
        var result = WindowPlacement.Map(new(400, 200, 800, 600), new(0, 0, 1600, 1000),
            new(-1600, 0, 1600, 1000));
        Assert.Equal(new DesktopRect(-1200, 200, 800, 600), result);
    }

    [Fact]
    public void WindowShrinksToFitSmallerTargetAndAvoidsTaskbar()
    {
        var result = WindowPlacement.Map(new(100, 200, 1800, 1000), new(0, 0, 3840, 2160),
            new(3840, 40, 1280, 680));
        Assert.Equal(new DesktopRect(3840, 40, 1280, 680), result);
    }

    [Fact]
    public void RelativePositionAdaptsToDifferentScreenSize()
    {
        var result = WindowPlacement.Map(new(600, 300, 800, 600), new(0, 0, 2000, 1200),
            new(2000, -1000, 1600, 1000));
        Assert.Equal(new DesktopRect(2400, -800, 800, 600), result);
    }

    [Fact]
    public void OffscreenWindowIsClampedWithinDestinationWorkArea()
    {
        var target = new DesktopRect(-1920, -1040, 1920, 1040);
        var result = WindowPlacement.Map(new(-500, 1500, 800, 600), new(0, 0, 1920, 1040), target);
        Assert.Equal(new DesktopRect(-1920, -600, 800, 600), result);
    }

    [Fact]
    public void SameWorkAreaPreservesPlacement()
    {
        var work = new DesktopRect(0, 48, 1920, 1032);
        var window = new DesktopRect(300, 148, 1000, 700);
        Assert.Equal(window, WindowPlacement.Map(window, work, work));
    }

    [Fact]
    public void EmptyWorkAreaIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            WindowPlacement.Map(new(0, 0, 800, 600), new(0, 0, 1920, 1080), new(0, 0, 0, 0)));
    }
}
