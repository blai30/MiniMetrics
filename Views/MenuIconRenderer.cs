using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace MiniMetrics.Views;

// Rasterizes a lucide-style stroke path into a small bitmap for use as a NativeMenuItem.Icon. The Win32
// tray menu is a managed Avalonia menu, so the bitmap is shown inside a themed MenuItem; the stroke color
// is baked in to match the current theme's menu text and is re-rendered when the theme changes.
internal static class MenuIconRenderer
{
    // The Fluent MenuItem icon presenter shows the bitmap in a 16-DIP Viewbox with StretchDirection
    // DownOnly, which scales by the bitmap's DIP size. Rendering at 96 DPI keeps the DIP size equal to the
    // pixel count, so a larger pixel bitmap is a larger DIP source that the Viewbox crisply scales down
    // into the 16px slot. (Raising the DPI instead reports a 16-DIP size that the presenter clips rather
    // than scales.) 32px stays crisp up to 200% display scaling.
    private const int RenderSize = 32;
    private const double LucideViewBox = 24.0;

    // Stroke width in the 24-unit lucide grid. The grid fills RenderSize and the menu Viewbox then scales
    // it to the 16px slot, so the on-screen stroke lands near lucide's intended weight at that size.
    private const double StrokeWidth = 2.0;

    public static Bitmap Render(string pathData, IBrush brush)
    {
        var geometry = Geometry.Parse(pathData);
        var bitmap = new RenderTargetBitmap(new(RenderSize, RenderSize));

        using var context = bitmap.CreateDrawingContext();

        // Fit the 24-unit grid into the bitmap, leaving a half-stroke inset so the round caps are not
        // clipped at the edges.
        double inset = StrokeWidth / 2.0;
        double scale = (RenderSize - StrokeWidth) / LucideViewBox;
        var pen = new Pen(brush, StrokeWidth)
        {
            LineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };

        using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(inset, inset)))
        {
            context.DrawGeometry(null, pen, geometry);
        }

        return bitmap;
    }
}
