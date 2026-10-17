using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using MirrorCast.Receiver.Models;
using MirrorCast.Receiver.Network;
using MirrorCast.Receiver.UI;
using MirrorCast.Receiver.Utils;
using MirrorCast.Receiver.Video;
using Serilog;

namespace MirrorCast.Receiver;

/// <summary>
/// Main window orchestrating network discovery, TCP streaming servers, video decoding,
/// high-performance frame rendering, and the dark glass UI/UX design system.
/// </summary>
public partial class MainWindow : Window
{
    private static readonly ILogger Logger = Log.ForContext<MainWindow>();

    private const int PrimaryStreamPort = 59100;
    private const int ControlPort = 59101;

    private readonly MdnsAdvertiser _mdnsAdvertiser;
    private readonly WiFiDirectService _wiFiDirectService;
    private readonly TcpStreamServer _tcpStreamServer;
    private readonly ControlSocketServer _controlSocketServer;
    private readonly H264Decoder _h264Decoder;
    private readonly FrameRenderer _frameRenderer;
    private readonly TrayIconManager _trayManager;

    private readonly DispatcherTimer _hudInactivityTimer;
    private bool _isFullscreen;
    private WindowState _previousWindowState = WindowState.Normal;
    private WindowStyle _previousWindowStyle = WindowStyle.None;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindow"/> class.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();

        Logger.Information("Initializing MainWindow components...");

        // 1. Initialize Network Services
        _mdnsAdvertiser = new MdnsAdvertiser(PrimaryStreamPort);
        _wiFiDirectService = new WiFiDirectService();
        _tcpStreamServer = new TcpStreamServer(PrimaryStreamPort);
        _controlSocketServer = new ControlSocketServer(ControlPort);

        // 2. Initialize Video Pipeline
        _h264Decoder = new H264Decoder(1920, 1080);
        _frameRenderer = new FrameRenderer(Dispatcher, 1920, 1080);

        // Bind image viewport to WriteableBitmap back-buffer
        VideoViewport.Source = _frameRenderer.Bitmap;
        _frameRenderer.BitmapCreated += newBitmap =>
        {
            Dispatcher.Invoke(() =>
            {
                VideoViewport.Source = newBitmap;
            });
        };

        // 3. Initialize Tray Icon
        _trayManager = new TrayIconManager(this);
        _trayManager.DisconnectClicked += DisconnectCurrentSession;

        // 4. Configure HUD Auto-Hide (3 seconds mouse inactivity)
        _hudInactivityTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(3)
        };
        _hudInactivityTimer.Tick += (s, e) => FadeOutHud();

        // 5. Wire Events Across Pipeline
        WirePipelineEvents();

        // 6. Start Services
        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
    }

    private void WirePipelineEvents()
    {
        // Network -> Handshake & Connection
        _tcpStreamServer.DeviceConnected += OnDeviceConnected;
        _tcpStreamServer.DeviceDisconnected += OnDeviceDisconnected;
        _tcpStreamServer.NalUnitReceived += OnNalUnitReceived;
        _tcpStreamServer.StreamDesyncDetected += OnStreamDesync;

        // Control Socket Signaling
        _controlSocketServer.StreamPaused += OnStreamPaused;
        _controlSocketServer.StreamResumed += OnStreamResumed;
        _controlSocketServer.ConfigUpdated += OnConfigUpdated;
        _controlSocketServer.DisconnectRequested += reason => DisconnectCurrentSession();

        // Video Decoder -> Frame Renderer
        _h264Decoder.FrameDecoded += (bgra, width, height, stride) =>
        {
            _frameRenderer.RenderFrame(bgra, width, height, stride);
        };

        // Frame Renderer FPS -> HUD Update
        _frameRenderer.RenderFpsTracker.FpsUpdated += fps =>
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, () =>
            {
                HudToolbar.UpdateFps(fps);
            });
        };
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            // Start TCP Listeners
            _tcpStreamServer.Start();
            _controlSocketServer.Start();

            // Start Discovery Services
            _mdnsAdvertiser.Start();
            _wiFiDirectService.Start();

            // Populate Startup Mode Details & QR Codes
            PopulateStartupDetails();

            Logger.Information("MirrorCast Receiver successfully initialized and listening on ports {StreamPort} and {ControlPort}.", PrimaryStreamPort, ControlPort);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to start one or more MirrorCast network services.");
        }
    }

    private void PopulateStartupDetails()
    {
        string localIp = NetworkHelper.GetPrimaryLocalIpAddress();
        string displayEndpoint = $"{localIp}:{PrimaryStreamPort}";
        LocalIpAddressText.Text = displayEndpoint;

        // Generate QR code for Same Wi-Fi LAN discovery
        string wifiPayload = $"mirrorcast://{localIp}:{PrimaryStreamPort}";
        WifiQrImage.Source = QrCodeGenerator.GenerateQrCodeImage(wifiPayload, 8);

        // Generate Wi-Fi Direct credentials & QR code
        DirectSsidText.Text = _wiFiDirectService.Ssid;
        DirectPassphraseText.Text = _wiFiDirectService.Passphrase;
        string directQrPayload = _wiFiDirectService.GetWifiQrPayload();
        DirectQrImage.Source = QrCodeGenerator.GenerateQrCodeImage(directQrPayload, 8);
    }

    #region Pipeline Event Handlers

    private void OnDeviceConnected(DeviceInfo device)
    {
        Dispatcher.Invoke(() =>
        {
            Logger.Information("Device connected: {DeviceName} ({Os})", device.Device, device.Os);

            TitleBarDeviceText.Text = $"{device.Device} ({device.Width}x{device.Height})";
            HudToolbar.SetDevice(device);
            _h264Decoder.UpdateResolution(device.Width, device.Height);

            // Animate transition to Streaming View
            StartupView.Visibility = Visibility.Collapsed;
            StreamingView.Visibility = Visibility.Visible;
            PausedOverlay.Visibility = Visibility.Collapsed;

            _trayManager.SetConnected(true, device.Device);

            // Reset HUD auto-hide
            ShowHud();
        });
    }

    private void OnDeviceDisconnected()
    {
        Dispatcher.Invoke(() =>
        {
            Logger.Information("Active device disconnected. Returning to discovery view.");

            TitleBarDeviceText.Text = string.Empty;
            StreamingView.Visibility = Visibility.Collapsed;
            StartupView.Visibility = Visibility.Visible;
            PausedOverlay.Visibility = Visibility.Collapsed;

            _trayManager.SetConnected(false);
            _frameRenderer.RenderFpsTracker.Reset();
            _hudInactivityTimer.Stop();

            if (_isFullscreen)
            {
                ExitFullscreen();
            }
        });
    }

    private void OnNalUnitReceived(byte[] nalPayload)
    {
        _h264Decoder.ProcessNalUnit(nalPayload);
    }

    private async void OnStreamDesync()
    {
        Logger.Warning("Stream desynchronization encountered. Requesting keyframe refresh...");
        await _controlSocketServer.RequestIdrKeyframeAsync();
    }

    private void OnStreamPaused()
    {
        Dispatcher.Invoke(() =>
        {
            PausedOverlay.Visibility = Visibility.Visible;
        });
    }

    private void OnStreamResumed()
    {
        Dispatcher.Invoke(() =>
        {
            PausedOverlay.Visibility = Visibility.Collapsed;
        });
    }

    private void OnConfigUpdated(ControlMessage msg)
    {
        Dispatcher.Invoke(() =>
        {
            if (msg.Width.HasValue && msg.Height.HasValue)
            {
                _h264Decoder.UpdateResolution(msg.Width.Value, msg.Height.Value);
                HudToolbar.UpdateResolution(msg.Width.Value, msg.Height.Value);
            }
        });
    }

    #endregion

    #region UI & Navigation Controls

    private void TabMode_Checked(object sender, RoutedEventArgs e)
    {
        if (SameWifiContent == null || WifiDirectContent == null) return;

        if (TabSameWifi.IsChecked == true)
        {
            SameWifiContent.Visibility = Visibility.Visible;
            WifiDirectContent.Visibility = Visibility.Collapsed;
        }
        else
        {
            SameWifiContent.Visibility = Visibility.Collapsed;
            WifiDirectContent.Visibility = Visibility.Visible;
        }
    }

    private void CopyIpAddress_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(LocalIpAddressText.Text);
            ShowToast("IP address copied to clipboard");
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Failed to copy IP address to clipboard");
        }
    }

    private void CustomTitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleMaximize();
        }
        else
        {
            DragMove();
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        ToggleMaximize();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ToggleMaximize()
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    #endregion

    #region HUD Interaction & Auto-Hide

    private void Window_MouseMove(object sender, MouseEventArgs e)
    {
        if (StreamingView.Visibility == Visibility.Visible)
        {
            ShowHud();
        }
    }

    private void ShowHud()
    {
        _hudInactivityTimer.Stop();

        if (HudToolbar.Opacity < 1.0)
        {
            var anim = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(200));
            HudToolbar.BeginAnimation(OpacityProperty, anim);
        }

        CustomTitleBar.Visibility = Visibility.Visible;
        _hudInactivityTimer.Start();
    }

    private void FadeOutHud()
    {
        _hudInactivityTimer.Stop();

        // Fade out toolbar
        var anim = new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(400));
        HudToolbar.BeginAnimation(OpacityProperty, anim);

        if (_isFullscreen)
        {
            CustomTitleBar.Visibility = Visibility.Collapsed;
        }
    }

    private void HudToolbar_ScreenshotClicked(object sender, RoutedEventArgs e)
    {
        string? savedPath = ScreenshotHelper.SaveFrameScreenshot(_frameRenderer.Bitmap);
        if (!string.IsNullOrEmpty(savedPath))
        {
            ShowToast("Screenshot saved to Desktop");
            _trayManager.ShowNotification("Screenshot Captured", "Saved to Desktop successfully.");
        }
    }

    private void HudToolbar_FullscreenClicked(object sender, RoutedEventArgs e)
    {
        ToggleFullscreen();
    }

    private void HudToolbar_DisconnectClicked(object sender, RoutedEventArgs e)
    {
        DisconnectCurrentSession();
    }

    private async void DisconnectCurrentSession()
    {
        try
        {
            await _controlSocketServer.SendDisconnectAsync("user_terminated");
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Failed to send disconnect control message");
        }

        _tcpStreamServer.DisconnectCurrentClient();
    }

    private void ShowToast(string message)
    {
        ToastText.Text = message;
        var fadeIn = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(200));
        var fadeOut = new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(300))
        {
            BeginTime = TimeSpan.FromSeconds(2.5)
        };

        var storyboard = new Storyboard();
        Storyboard.SetTarget(fadeIn, ToastBanner);
        Storyboard.SetTargetProperty(fadeIn, new PropertyPath(OpacityProperty));
        Storyboard.SetTarget(fadeOut, ToastBanner);
        Storyboard.SetTargetProperty(fadeOut, new PropertyPath(OpacityProperty));

        storyboard.Children.Add(fadeIn);
        storyboard.Children.Add(fadeOut);
        storyboard.Begin();
    }

    #endregion

    #region Fullscreen Handling

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F11)
        {
            ToggleFullscreen();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && _isFullscreen)
        {
            ExitFullscreen();
            e.Handled = true;
        }
    }

    private void ToggleFullscreen()
    {
        if (_isFullscreen)
        {
            ExitFullscreen();
        }
        else
        {
            EnterFullscreen();
        }
    }

    private void EnterFullscreen()
    {
        _previousWindowState = WindowState;
        _previousWindowStyle = WindowStyle;

        WindowStyle = WindowStyle.None;
        WindowState = WindowState.Maximized;
        _isFullscreen = true;

        CustomTitleBar.Visibility = Visibility.Collapsed;
        ShowToast("Press F11 or Esc to exit fullscreen");
    }

    private void ExitFullscreen()
    {
        WindowState = _previousWindowState;
        WindowStyle = _previousWindowStyle;
        _isFullscreen = false;

        CustomTitleBar.Visibility = Visibility.Visible;
    }

    #endregion

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        Logger.Information("MainWindow closing. Shutting down receiver subsystems...");

        _hudInactivityTimer.Stop();
        _trayManager.Dispose();

        _mdnsAdvertiser.Dispose();
        _wiFiDirectService.Dispose();
        _tcpStreamServer.Dispose();
        _controlSocketServer.Dispose();
        _h264Decoder.Dispose();
    }
}
