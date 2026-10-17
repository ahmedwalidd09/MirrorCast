using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace MirrorCast.Receiver.UI.Converters;

/// <summary>
/// Converts real-time FPS numbers to design-token status brushes:
/// Green (#30D158) for >= 55, Yellow (#FF9F0A) for 30-54, Red (#FF453A) for &lt; 30.
/// </summary>
public class FpsToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush GreenBrush = new((Color)ColorConverter.ConvertFromString("#30D158"));
    private static readonly SolidColorBrush YellowBrush = new((Color)ColorConverter.ConvertFromString("#FF9F0A"));
    private static readonly SolidColorBrush RedBrush = new((Color)ColorConverter.ConvertFromString("#FF453A"));
    private static readonly SolidColorBrush DefaultBrush = new((Color)ColorConverter.ConvertFromString("#8E8E93"));

    static FpsToBrushConverter()
    {
        GreenBrush.Freeze();
        YellowBrush.Freeze();
        RedBrush.Freeze();
        DefaultBrush.Freeze();
    }

    /// <summary>
    /// Converts FPS numeric value to SolidColorBrush.
    /// </summary>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        double fps = 0;
        if (value is double d) fps = d;
        else if (value is int i) fps = i;
        else if (value is float f) fps = f;

        if (fps <= 0) return DefaultBrush;
        if (fps >= 55.0) return GreenBrush;
        if (fps >= 30.0) return YellowBrush;
        return RedBrush;
    }

    /// <summary>
    /// Not supported.
    /// </summary>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
