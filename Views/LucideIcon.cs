using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace MiniMetrics.Views;

// Draws a single stroked lucide vector icon, themed by its (inherited) Foreground. Used wherever a slot
// accepts arbitrary content: the settings nav rail, standalone buttons, and dialog headers. The settings
// expander/nav IconSource slots cannot host a stroked vector, so those use the rasterizing converter
// instead. See LucideIcons for the path data and the shared draw routine.
public sealed class LucideIcon : Control
{
    // Stroke width in the 24-unit lucide grid; LucideIcons.Draw scales it down with the geometry.
    private const double StrokeWidth = 2.0;

    public static readonly StyledProperty<string?> SymbolProperty =
        AvaloniaProperty.Register<LucideIcon, string?>(nameof(Symbol));

    // Reuse the inherited text foreground so the icon themes with surrounding text and can be overridden
    // by callers (the dialogs set an accent or critical brush).
    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<LucideIcon>();

    static LucideIcon()
    {
        AffectsRender<LucideIcon>(SymbolProperty, ForegroundProperty);
    }

    public string? Symbol
    {
        get => GetValue(SymbolProperty);
        set => SetValue(SymbolProperty, value);
    }

    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize) => new(16, 16);

    public override void Render(DrawingContext context)
    {
        if (Symbol is not { } symbol) return;
        if (Foreground is not { } brush) return;

        var geometry = LucideIcons.Get(symbol);
        if (geometry is null) return;

        double box = System.Math.Min(Bounds.Width, Bounds.Height);
        if (box <= 0) return;

        var pen = new Pen(brush, StrokeWidth)
        {
            LineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };
        LucideIcons.Draw(context, geometry, pen, box);
    }
}
