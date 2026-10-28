using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace MiniMetrics.Views;

// A TextBlock that reserves the width its text's digit shape would need with tabular figures but still
// draws with the font's natural proportional ones. Proportional digits differ in advance width (Inter's
// "1" is far narrower than its "8"), so a widget sized to its content resizes on almost every clock
// tick. Reserving the width of the text with every digit replaced by "0" ties the measured width to the
// shape of the text rather than to which digits happen to be showing (even tabular figures are not all
// equal: Inter's tabular "4" is narrower), which holds the widget still without imposing the evenly
// spaced look of tabular figures on the reader. The reserved slack lands on whichever side
// TextAlignment does not pin the text to.
public class StableWidthTextBlock : TextBlock
{
    private static readonly FontFeatureCollection TabularFigures = FontFeatureCollection.Parse("+tnum");

    // The reservation for the last shape measured. A clock tick only changes digits, so the shape (and
    // the reservation) stays the same and the text is not shaped a second time.
    private string? _shape;
    private Size _shapeConstraint;
    private double _reserved;

    protected override Size MeasureOverride(Size availableSize)
    {
        var natural = base.MeasureOverride(availableSize);
        if (string.IsNullOrEmpty(Text)) return natural;

        string shape = ShapeOf(Text);
        if (shape != _shape || availableSize != _shapeConstraint)
        {
            var constraint = availableSize.Deflate(Padding);
            using var tabular = new TextLayout(
                shape,
                new(FontFamily, FontStyle, FontWeight, FontStretch),
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

            _shape = shape;
            _shapeConstraint = availableSize;
            _reserved = tabular.WidthIncludingTrailingWhitespace + Padding.Left + Padding.Right;
        }

        return new(Math.Max(natural.Width, _reserved), natural.Height);
    }

    // Any input other than the text itself (font, size, weight, alignment, ...) can change the
    // reservation, so it is recomputed on the next measure.
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != TextProperty) _shape = null;
    }

    private static string ShapeOf(string text) => string.Create(text.Length, text, static (shape, source) =>
    {
        for (int i = 0; i < shape.Length; i++)
        {
            shape[i] = char.IsAsciiDigit(source[i]) ? '0' : source[i];
        }
    });
}
