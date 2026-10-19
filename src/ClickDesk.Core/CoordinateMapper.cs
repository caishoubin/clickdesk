namespace ClickDesk.Core;

public static class CoordinateMapper
{
    // SendInput divides the virtual desktop into 65,536 units. Aim at each pixel's center.
    public static (int X, int Y) ToAbsolute(int x, int y, DesktopBounds bounds)
    {
        if (!bounds.Contains(x, y)) throw new ArgumentOutOfRangeException(nameof(x), "The click position is outside the current desktop.");
        return ((int)((((long)x - bounds.Left) * 65536 + 32768) / bounds.Width),
            (int)((((long)y - bounds.Top) * 65536 + 32768) / bounds.Height));
    }
}
