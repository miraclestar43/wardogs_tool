namespace WardogsTool.Core.Magnifier;

/// <summary>A screen rectangle in physical pixels (Left/Top inclusive, Right/Bottom exclusive).</summary>
public readonly record struct PixelRect(int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left;
    public int Height => Bottom - Top;
    public double CenterX => Left + Width / 2.0;
    public double CenterY => Top + Height / 2.0;
}

/// <summary>Where the lens goes and which screen area it shows.</summary>
/// <param name="Lens">The overlay window, centred on the monitor.</param>
/// <param name="Source">The screen area that is enlarged: the lens size divided by the zoom, same centre.</param>
public readonly record struct MagnifierLayout(PixelRect Lens, PixelRect Source, double Zoom);

/// <summary>
/// Pure geometry for the centre-screen magnifier. It only decides which on-screen pixels to
/// enlarge; it knows nothing about the game.
/// </summary>
public static class MagnifierGeometry
{
    public static readonly IReadOnlyList<double> ZoomChoices = [1.5, 2.0, 2.5, 3.0, 4.0];
    public const double DefaultZoom = 2.0;
    public const int DefaultLensWidth = 600;
    public const int DefaultLensHeight = 400;

    /// <summary>Snaps a stored zoom to the nearest offered choice (bad values → 2.0x).</summary>
    public static double NormalizeZoom(double zoom) =>
        double.IsFinite(zoom) ? ZoomChoices.MinBy(z => Math.Abs(z - zoom)) : DefaultZoom;

    /// <summary>
    /// Lens centred on <paramref name="monitor"/> (shrunk to fit a smaller monitor), and the source
    /// area of lens/zoom pixels with the same centre. Works with negative monitor origins
    /// (monitors left of / above the primary one).
    /// </summary>
    public static MagnifierLayout Compute(PixelRect monitor, double zoom, int lensWidth = DefaultLensWidth, int lensHeight = DefaultLensHeight)
    {
        if (monitor.Width <= 0 || monitor.Height <= 0)
            throw new ArgumentException("Monitor rectangle is empty.", nameof(monitor));
        if (!double.IsFinite(zoom) || zoom < 1)
            throw new ArgumentOutOfRangeException(nameof(zoom), zoom, "Zoom must be at least 1.");

        var w = Math.Min(lensWidth, monitor.Width);
        var h = Math.Min(lensHeight, monitor.Height);
        var lens = Centered(monitor, w, h);

        // The magnifier scales source → lens by exactly `zoom`, so the source is lens / zoom.
        var sw = Math.Max(1, (int)Math.Round(w / zoom));
        var sh = Math.Max(1, (int)Math.Round(h / zoom));
        var source = Centered(monitor, sw, sh);
        return new MagnifierLayout(lens, source, zoom);
    }

    private static PixelRect Centered(PixelRect monitor, int width, int height)
    {
        var left = monitor.Left + (monitor.Width - width) / 2;
        var top = monitor.Top + (monitor.Height - height) / 2;
        return new PixelRect(left, top, left + width, top + height);
    }
}
