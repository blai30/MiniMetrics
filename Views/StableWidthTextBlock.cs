using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace MiniMetrics.Views;

// A TextBlock that reserves the width its text would need with tabular figures but still draws with
// the font's natural proportional ones. Proportional digits differ in advance width (Inter's "1" is
// far narrower than its "8"), so a widget sized to its content resizes on almost every clock tick.
// Reserving the widest advance each digit could take ties the measured width to the shape of the
// text rather than to which digits happen to be showing, which holds the widget still without
// imposing the evenly spaced look of tabular figures on the reader. The reserved slack lands on
// whichever side TextAlignment does not pin the text to.
public class StableWidthTextBlock : TextBlock
{
    private static readonly FontFeatureCollection TabularFigures = FontFeatureCollection.Parse("+tnum");

    protected override Size MeasureOverride(Size availableSize)
    {
        var natural = base.MeasureOverride(availableSize);
        if (string.IsNullOrEmpty(Text)) return natural;

        var constraint = availableSize.Deflate(Padding);
        var tabular = new TextLayout(
            Text,
            new Typeface(FontFamily, FontStyle, FontWeight, FontStretch),
            FontSize,
            Foreground,
            TextAlignment,
            TextWrapping,
            TextTrimming,
            TextDecorations,
            FlowDirection,
            constraint.Width,
            constraint.Height,
            LineHeight,
            LetterSpacing,
            MaxLines,
            TabularFigures);

        double reserved = tabular.WidthIncludingTrailingWhitespace + Padding.Left + Padding.Right;
        return new(Math.Max(natural.Width, reserved), natural.Height);
    }
}
