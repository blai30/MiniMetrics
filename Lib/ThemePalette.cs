namespace MiniMetrics.Lib;

// Light and dark hex values for the widget accent colors (metric bar gradients and temperature heat).
// Pure and Avalonia-free; the converters turn the hex strings into brushes. The dark values are the
// app's original colors; the light values darken the pale ends so they stay legible on a light card.
public static class ThemePalette
{
    public static (string From, string To) BarGradient(RowColor color, bool isDark) => (color, isDark) switch
    {
        (RowColor.Cyan, true) => ("#0EA5E9", "#67E8F9"),
        (RowColor.Green, true) => ("#10B981", "#6EE7B7"),
        (RowColor.Amber, true) => ("#F59E0B", "#FCD34D"),
        (RowColor.Violet, true) => ("#8B5CF6", "#C4B5FD"),
        (RowColor.Cyan, false) => ("#0284C7", "#0EA5E9"),
        (RowColor.Green, false) => ("#059669", "#10B981"),
        (RowColor.Amber, false) => ("#D97706", "#F59E0B"),
        (RowColor.Violet, false) => ("#7C3AED", "#8B5CF6"),
        (_, true) => ("#0EA5E9", "#67E8F9"),
        (_, false) => ("#0284C7", "#0EA5E9")
    };

    public static string TempColor(TempLevel level, bool isDark) => (level, isDark) switch
    {
        (TempLevel.Frigid, true) => "#7DD3FC",
        (TempLevel.Cold, true) => "#2DD4BF",
        (TempLevel.Cool, true) => "#5ED6A8",
        (TempLevel.Warm, true) => "#F5B544",
        (TempLevel.Hot, true) => "#FB923C",
        (TempLevel.Critical, true) => "#F87171",
        (TempLevel.Frigid, false) => "#0284C7",
        (TempLevel.Cold, false) => "#0D9488",
        (TempLevel.Cool, false) => "#059669",
        (TempLevel.Warm, false) => "#B45309",
        (TempLevel.Hot, false) => "#C2410C",
        (TempLevel.Critical, false) => "#DC2626",
        (_, true) => "#9AA1AC",
        (_, false) => "#6B7280"
    };
}
