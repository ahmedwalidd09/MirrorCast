using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MirrorCast.Receiver.UI.Converters;

/// <summary>
/// Converts boolean values to WPF Visibility enumerations with optional inversion.
/// </summary>
public class BooleanToVisibilityConverter : IValueConverter
{
    /// <summary>
    /// Gets or sets a value indicating whether true should map to Collapsed/Hidden.
    /// </summary>
    public bool Invert { get; set; }

    /// <summary>
    /// Converts a boolean to Visibility.
    /// </summary>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool b = value is bool val && val;
        if (Invert) b = !b;
        return b ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// Converts Visibility back to boolean.
    /// </summary>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool isVisible = value is Visibility v && v == Visibility.Visible;
        return Invert ? !isVisible : isVisible;
    }
}
