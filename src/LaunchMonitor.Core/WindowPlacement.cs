namespace LaunchMonitor.Core;

public readonly record struct DesktopRect(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;
    public int Bottom => Y + Height;
}

public static class WindowPlacement
{
    /// <summary>Preserve relative position and pixel size, shrinking only to fit the destination work area.</summary>
    public static DesktopRect Map(DesktopRect window, DesktopRect source, DesktopRect target)
    {
        if (source.Width <= 0 || source.Height <= 0 || target.Width <= 0 || target.Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(target), "Monitor work areas must be nonempty.");

        var width = Math.Clamp(window.Width, 1, target.Width);
        var height = Math.Clamp(window.Height, 1, target.Height);
        var xFraction = source.Width > window.Width
            ? Math.Clamp((double)(window.X - source.X) / (source.Width - window.Width), 0, 1)
            : 0;
        var yFraction = source.Height > window.Height
            ? Math.Clamp((double)(window.Y - source.Y) / (source.Height - window.Height), 0, 1)
            : 0;
        return new(
            target.X + (int)Math.Round(xFraction * (target.Width - width)),
            target.Y + (int)Math.Round(yFraction * (target.Height - height)),
            width,
            height);
    }
}
