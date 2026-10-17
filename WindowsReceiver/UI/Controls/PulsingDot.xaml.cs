using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MirrorCast.Receiver.UI.Controls;

/// <summary>
/// Interaction logic for PulsingDot.xaml.
/// Displays an 8px indicator dot with a smooth repeating pulse aura.
/// </summary>
public partial class PulsingDot : UserControl
{
    /// <summary>
    /// Identifies the DotBrush dependency property.
    /// </summary>
    public static readonly DependencyProperty DotBrushProperty =
        DependencyProperty.Register(
            nameof(DotBrush),
            typeof(Brush),
            typeof(PulsingDot),
            new PropertyMetadata(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#30D158"))));

    /// <summary>
    /// Gets or sets the brush color used for the core dot and animated pulsing aura.
    /// </summary>
    public Brush DotBrush
    {
        get => (Brush)GetValue(DotBrushProperty);
        set => SetValue(DotBrushProperty, value);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PulsingDot"/> class.
    /// </summary>
    public PulsingDot()
    {
        InitializeComponent();
    }
}
