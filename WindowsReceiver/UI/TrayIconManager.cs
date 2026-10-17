using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using Serilog;

namespace MirrorCast.Receiver.UI;

/// <summary>
/// Manages Windows Taskbar System Tray NotifyIcon integration, notifications, and context menu.
/// </summary>
public class TrayIconManager : IDisposable
{
    private static readonly ILogger Logger = Log.ForContext<TrayIconManager>();

    private readonly Window _mainWindow;
    private NotifyIcon? _notifyIcon;
    private readonly ContextMenuStrip _contextMenu = new();
    private readonly ToolStripMenuItem _showItem;
    private readonly ToolStripMenuItem _disconnectItem;
    private readonly ToolStripMenuItem _exitItem;

    /// <summary>
    /// Event raised when the user clicks 'Disconnect' from the tray context menu.
    /// </summary>
    public event Action? DisconnectClicked;

    /// <summary>
    /// Initializes a new instance of the <see cref="TrayIconManager"/> class.
    /// </summary>
    /// <param name="mainWindow">The primary WPF application window.</param>
    public TrayIconManager(Window mainWindow)
    {
        _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));

        _showItem = new ToolStripMenuItem("Show MirrorCast", null, (s, e) => ShowWindow());
        _disconnectItem = new ToolStripMenuItem("Disconnect", null, (s, e) => DisconnectClicked?.Invoke())
        {
            Enabled = false
        };
        _exitItem = new ToolStripMenuItem("Exit", null, (s, e) => ExitApplication());

        _contextMenu.Items.Add(_showItem);
        _contextMenu.Items.Add(new ToolStripSeparator());
        _contextMenu.Items.Add(_disconnectItem);
        _contextMenu.Items.Add(new ToolStripSeparator());
        _contextMenu.Items.Add(_exitItem);

        InitializeTrayIcon();
    }

    private void InitializeTrayIcon()
    {
        try
        {
            // Generate a sleek dark/silver programmatic icon (16x16)
            var bmp = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.FromArgb(13, 13, 13)); // #0D0D0D
                using var pen = new Pen(Color.FromArgb(200, 200, 204), 1.5f); // #C8C8CC
                g.DrawRectangle(pen, 2, 2, 11, 8);
                using var brush = new SolidBrush(Color.FromArgb(48, 209, 88)); // #30D158
                g.FillEllipse(brush, 6, 11, 3, 3);
            }
            nint hIcon = bmp.GetHicon();
            Icon icon = Icon.FromHandle(hIcon);

            _notifyIcon = new NotifyIcon
            {
                Icon = icon,
                Text = "MirrorCast Receiver",
                Visible = true,
                ContextMenuStrip = _contextMenu
            };

            _notifyIcon.DoubleClick += (s, e) => ShowWindow();
            Logger.Information("Tray icon initialized successfully.");
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Failed to initialize custom tray icon; using system default.");
            _notifyIcon = new NotifyIcon
            {
                Icon = SystemIcons.Application,
                Text = "MirrorCast Receiver",
                Visible = true,
                ContextMenuStrip = _contextMenu
            };
            _notifyIcon.DoubleClick += (s, e) => ShowWindow();
        }
    }

    /// <summary>
    /// Updates the state of the Disconnect context menu item depending on stream connection.
    /// </summary>
    public void SetConnected(bool isConnected, string? deviceName = null)
    {
        if (_notifyIcon == null) return;

        _disconnectItem.Enabled = isConnected;
        if (isConnected && !string.IsNullOrWhiteSpace(deviceName))
        {
            _notifyIcon.Text = $"MirrorCast: Streaming from {deviceName}";
        }
        else
        {
            _notifyIcon.Text = "MirrorCast: Ready to connect";
        }
    }

    /// <summary>
    /// Displays a temporary system balloon notification.
    /// </summary>
    public void ShowNotification(string title, string text, ToolTipIcon icon = ToolTipIcon.Info)
    {
        try
        {
            _notifyIcon?.ShowBalloonTip(3000, title, text, icon);
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Failed to show balloon tip");
        }
    }

    private void ShowWindow()
    {
        _mainWindow.Dispatcher.Invoke(() =>
        {
            if (_mainWindow.WindowState == WindowState.Minimized)
            {
                _mainWindow.WindowState = WindowState.Normal;
            }
            _mainWindow.Show();
            _mainWindow.Activate();
        });
    }

    private void ExitApplication()
    {
        _mainWindow.Dispatcher.Invoke(() =>
        {
            System.Windows.Application.Current.Shutdown();
        });
    }

    /// <summary>
    /// Disposes the tray icon.
    /// </summary>
    public void Dispose()
    {
        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }
        _contextMenu.Dispose();
        GC.SuppressFinalize(this);
    }
}
