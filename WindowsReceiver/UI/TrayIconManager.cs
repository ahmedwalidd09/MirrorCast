using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Serilog;

namespace MirrorCast.Receiver.UI;

/// <summary>
/// Manages Windows Taskbar System Tray NotifyIcon integration using pure Win32 Shell_NotifyIcon
/// and WPF context menus, eliminating any Windows Forms dependency.
/// </summary>
public class TrayIconManager : IDisposable
{
    private static readonly ILogger Logger = Log.ForContext<TrayIconManager>();

    private const int WM_USER = 0x0400;
    private const int WM_TRAYICON = WM_USER + 101;
    private const int WM_LBUTTONDBLCLK = 0x0203;
    private const int WM_RBUTTONUP = 0x0205;

    private const uint NIM_ADD = 0x00000000;
    private const uint NIM_MODIFY = 0x00000001;
    private const uint NIM_DELETE = 0x00000002;

    private const uint NIF_MESSAGE = 0x00000001;
    private const uint NIF_ICON = 0x00000002;
    private const uint NIF_TIP = 0x00000004;
    private const uint NIF_INFO = 0x00000010;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        public uint cbSize;
        public nint hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public nint hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public uint uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public uint dwInfoFlags;
        public Guid guidItem;
        public nint hBalloonIcon;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA lpData);

    [DllImport("user32.dll")]
    private static extern nint LoadIcon(nint hInstance, nint lpIconName);

    private readonly Window _mainWindow;
    private readonly ContextMenu _contextMenu = new();
    private readonly MenuItem _showItem;
    private readonly MenuItem _disconnectItem;
    private readonly MenuItem _exitItem;

    private HwndSource? _hwndSource;
    private bool _isAdded;
    private NOTIFYICONDATA _nid;

    /// <summary>
    /// Event raised when the user clicks 'Disconnect' from the tray context menu.
    /// </summary>
    public event Action? DisconnectClicked;

    /// <summary>
    /// Initializes a new instance of the <see cref="TrayIconManager"/> class.
    /// </summary>
    public TrayIconManager(Window mainWindow)
    {
        _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));

        _showItem = new MenuItem { Header = "Show MirrorCast" };
        _showItem.Click += (s, e) => ShowWindow();

        _disconnectItem = new MenuItem { Header = "Disconnect", IsEnabled = false };
        _disconnectItem.Click += (s, e) => DisconnectClicked?.Invoke();

        _exitItem = new MenuItem { Header = "Exit" };
        _exitItem.Click += (s, e) => ExitApplication();

        _contextMenu.Items.Add(_showItem);
        _contextMenu.Items.Add(new Separator());
        _contextMenu.Items.Add(_disconnectItem);
        _contextMenu.Items.Add(new Separator());
        _contextMenu.Items.Add(_exitItem);

        if (_mainWindow.IsLoaded)
        {
            InitializeTrayIcon();
        }
        else
        {
            _mainWindow.Loaded += (s, e) => InitializeTrayIcon();
        }
    }

    private void InitializeTrayIcon()
    {
        try
        {
            var helper = new WindowInteropHelper(_mainWindow);
            nint hwnd = helper.Handle;

            _hwndSource = HwndSource.FromHwnd(hwnd);
            _hwndSource?.AddHook(WndProc);

            _nid = new NOTIFYICONDATA
            {
                cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(),
                hWnd = hwnd,
                uID = 1001,
                uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
                uCallbackMessage = WM_TRAYICON,
                hIcon = LoadIcon(0, 32512), // IDI_APPLICATION
                szTip = "MirrorCast: Ready to connect",
                szInfo = string.Empty,
                szInfoTitle = string.Empty
            };

            _isAdded = Shell_NotifyIcon(NIM_ADD, ref _nid);
            Logger.Information("Win32 TrayIcon initialized (Added={Added})", _isAdded);
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Failed to initialize Win32 tray icon");
        }
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == WM_TRAYICON)
        {
            int action = lParam.ToInt32();
            if (action == WM_LBUTTONDBLCLK)
            {
                ShowWindow();
                handled = true;
            }
            else if (action == WM_RBUTTONUP)
            {
                _contextMenu.IsOpen = true;
                handled = true;
            }
        }
        return 0;
    }

    /// <summary>
    /// Updates the state of the Disconnect context menu item depending on stream connection.
    /// </summary>
    public void SetConnected(bool isConnected, string? deviceName = null)
    {
        _mainWindow.Dispatcher.Invoke(() =>
        {
            _disconnectItem.IsEnabled = isConnected;
            if (_isAdded)
            {
                _nid.szTip = isConnected && !string.IsNullOrWhiteSpace(deviceName)
                    ? $"MirrorCast: Streaming from {deviceName}"
                    : "MirrorCast: Ready to connect";
                Shell_NotifyIcon(NIM_MODIFY, ref _nid);
            }
        });
    }

    /// <summary>
    /// Displays a temporary system balloon notification.
    /// </summary>
    public void ShowNotification(string title, string text)
    {
        if (!_isAdded) return;

        _mainWindow.Dispatcher.Invoke(() =>
        {
            try
            {
                _nid.uFlags |= NIF_INFO;
                _nid.szInfoTitle = title;
                _nid.szInfo = text;
                _nid.dwInfoFlags = 0x00000001; // NIIF_INFO
                _nid.uTimeoutOrVersion = 3000;
                Shell_NotifyIcon(NIM_MODIFY, ref _nid);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Failed to show balloon notification");
            }
        });
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
            Application.Current.Shutdown();
        });
    }

    /// <summary>
    /// Removes the tray icon and releases unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        if (_isAdded)
        {
            Shell_NotifyIcon(NIM_DELETE, ref _nid);
            _isAdded = false;
        }

        if (_hwndSource != null)
        {
            _hwndSource.RemoveHook(WndProc);
            _hwndSource = null;
        }

        GC.SuppressFinalize(this);
    }
}
