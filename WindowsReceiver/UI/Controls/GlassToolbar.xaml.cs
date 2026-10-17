using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MirrorCast.Receiver.Models;

namespace MirrorCast.Receiver.UI.Controls;

/// <summary>
/// Interaction logic for GlassToolbar.xaml.
/// Translucent floating HUD displaying device metadata, real-time FPS, screenshot button, fullscreen toggle, and disconnect button.
/// </summary>
public partial class GlassToolbar : System.Windows.Controls.UserControl
{
    private static readonly SolidColorBrush GreenBrush = new((Color)ColorConverter.ConvertFromString("#30D158"));
    private static readonly SolidColorBrush YellowBrush = new((Color)ColorConverter.ConvertFromString("#FF9F0A"));
    private static readonly SolidColorBrush RedBrush = new((Color)ColorConverter.ConvertFromString("#FF453A"));
    private static readonly SolidColorBrush SilverBrush = new((Color)ColorConverter.ConvertFromString("#8E8E93"));

    static GlassToolbar()
    {
        GreenBrush.Freeze();
        YellowBrush.Freeze();
        RedBrush.Freeze();
        SilverBrush.Freeze();
    }

    /// <summary>
    /// Event raised when the user clicks the Screenshot button.
    /// </summary>
    public event RoutedEventHandler? ScreenshotClicked;

    /// <summary>
    /// Event raised when the user clicks the Fullscreen toggle button.
    /// </summary>
    public event RoutedEventHandler? FullscreenClicked;

    /// <summary>
    /// Event raised when the user clicks the Disconnect button.
    /// </summary>
    public event RoutedEventHandler? DisconnectClicked;

    /// <summary>
    /// Initializes a new instance of the <see cref="GlassToolbar"/> class.
    /// </summary>
    public GlassToolbar()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Configures the toolbar for a newly connected device.
    /// </summary>
    public void SetDevice(DeviceInfo info)
    {
        DeviceNameText.Text = string.IsNullOrWhiteSpace(info.Device) ? "Connected Device" : info.Device;
        bool isIos = info.IsIos;

        AppleIcon.Visibility = isIos ? Visibility.Visible : Visibility.Collapsed;
        AndroidIcon.Visibility = isIos ? Visibility.Collapsed : Visibility.Visible;

        StreamModeText.Text = string.Equals(info.Mode, "wifi-direct", StringComparison.OrdinalIgnoreCase) ? "Wi-Fi Direct" : "Wi-Fi LAN";
        ResolutionText.Text = $"{info.Width}x{info.Height}";
    }

    /// <summary>
    /// Updates the displayed FPS counter and status color.
    /// </summary>
    public void UpdateFps(double fps)
    {
        int rounded = (int)Math.Round(fps);
        FpsValueText.Text = rounded.ToString();

        if (rounded >= 55)
        {
            FpsValueText.Foreground = GreenBrush;
        }
        else if (rounded >= 30)
        {
            FpsValueText.Foreground = YellowBrush;
        }
        else if (rounded > 0)
        {
            FpsValueText.Foreground = RedBrush;
        }
        else
        {
            FpsValueText.Foreground = SilverBrush;
        }
    }

    /// <summary>
    /// Updates the displayed stream resolution string.
    /// </summary>
    public void UpdateResolution(int width, int height)
    {
        ResolutionText.Text = $"{width}x{height}";
    }

    private void ScreenshotBtn_Click(object sender, RoutedEventArgs e)
    {
        ScreenshotClicked?.Invoke(this, e);
    }

    private void FullscreenBtn_Click(object sender, RoutedEventArgs e)
    {
        FullscreenClicked?.Invoke(this, e);
    }

    private void DisconnectBtn_Click(object sender, RoutedEventArgs e)
    {
        DisconnectClicked?.Invoke(this, e);
    }
}
