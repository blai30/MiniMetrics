using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace MiniMetrics.Views;

// Rasterizes a catalog lucide icon into a small bitmap for use as a NativeMenuItem.Icon (tray) or an
// FAImageIconSource (settings-card slots). The bitmap is shown inside a themed control; the stroke color
// is baked in to match the current theme and is re-rendered when the theme changes.
internal static class MenuIconRenderer
{
    // The Fluent icon presenter shows the bitmap in a 16-DIP Viewbox with StretchDirection DownOnly, which
    // scales by the bitmap's DIP size. Rendering at 96 DPI keeps the DIP size equal to the pixel count, so a
    // larger pixel bitmap is a larger DIP source the Viewbox crisply scales into the slot. 32px stays crisp
    // up to 200% display scaling.
    private const int RenderSize = 32;

    // Default stroke width in the 24-unit lucide grid (lucide's own weight); the menu Viewbox then scales
    // the bitmap to the slot, so the on-screen stroke lands near that weight at the rendered size. Callers
    // can pass a thinner value (the settings cards do).
    private const double DefaultStrokeWidth = 2.0;

    public static Bitmap Render(string iconName, IBrush brush, double strokeWidth = DefaultStrokeWidth)
    {
        var bitmap = new RenderTargetBitmap(new(RenderSize, RenderSize));

        using var context = bitmap.CreateDrawingContext();

        var geometry = LucideIcons.Get(iconName);
        if (geometry is not null)
        {
            var pen = new Pen(brush, strokeWidth)
            {
                LineCap = PenLineCap.Round,
                LineJoin = PenLineJoin.Round
            };
            LucideIcons.Draw(context, geometry, pen, RenderSize);
        }

        return bitmap;
    }
}
