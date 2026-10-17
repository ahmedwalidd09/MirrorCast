using System.Windows;
using System.Windows.Media;
using Color = System.Windows.Media.Color;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Serilog;

namespace MirrorCast.Receiver.Video;

/// <summary>
/// High-performance WPF video frame renderer.
/// Renders BGRA32 pixel buffers directly into a <see cref="WriteableBitmap"/> back-buffer using
/// hardware-backed Lock/AddDirtyRect updates to maintain smooth 60 FPS playback with minimal latency.
/// </summary>
public class FrameRenderer
{
    private static readonly ILogger Logger = Log.ForContext<FrameRenderer>();

    private readonly Dispatcher _dispatcher;
    private WriteableBitmap? _bitmap;
    private int _width;
    private int _height;
    private bool _isRendering;
    private readonly object _lock = new();

    /// <summary>
    /// Event raised when the underlying WriteableBitmap instance is reallocated or initialized.
    /// </summary>
    public event Action<WriteableBitmap>? BitmapCreated;

    /// <summary>
    /// Gets the current active WriteableBitmap source.
    /// </summary>
    public WriteableBitmap? Bitmap => _bitmap;

    /// <summary>
    /// Gets the associated FPS tracker for rendered frames.
    /// </summary>
    public FpsTracker RenderFpsTracker { get; } = new(1.0);

    /// <summary>
    /// Initializes a new instance of the <see cref="FrameRenderer"/> class.
    /// </summary>
    /// <param name="dispatcher">The UI thread dispatcher.</param>
    /// <param name="initialWidth">Initial width in pixels (e.g. 1920).</param>
    /// <param name="initialHeight">Initial height in pixels (e.g. 1080).</param>
    public FrameRenderer(Dispatcher dispatcher, int initialWidth = 1920, int initialHeight = 1080)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _width = Math.Max(1, initialWidth);
        _height = Math.Max(1, initialHeight);

        _dispatcher.Invoke(InitializeBitmap);
    }

    private void InitializeBitmap()
    {
        try
        {
            _bitmap = new WriteableBitmap(
                _width,
                _height,
                96,
                96,
                PixelFormats.Bgra32,
                null);

            BitmapCreated?.Invoke(_bitmap);
            Logger.Information("FrameRenderer: Created {Width}x{Height} Bgra32 WriteableBitmap", _width, _height);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to create WriteableBitmap of size {W}x{H}", _width, _height);
        }
    }

    /// <summary>
    /// Resizes the render target surface to match incoming video stream dimensions.
    /// </summary>
    public void Resize(int width, int height)
    {
        if (width <= 0 || height <= 0) return;

        lock (_lock)
        {
            if (_width == width && _height == height) return;

            _width = width;
            _height = height;
        }

        _dispatcher.BeginInvoke(DispatcherPriority.Render, () =>
        {
            InitializeBitmap();
        });
    }

    /// <summary>
    /// Pushes a decoded frame buffer to the WriteableBitmap back-buffer.
    /// </summary>
    /// <param name="bgraPixels">The raw 32-bit BGRA pixel byte array.</param>
    /// <param name="frameWidth">Frame pixel width.</param>
    /// <param name="frameHeight">Frame pixel height.</param>
    /// <param name="stride">Byte stride per row (typically width * 4).</param>
    public void RenderFrame(byte[] bgraPixels, int frameWidth, int frameHeight, int stride)
    {
        if (bgraPixels == null || bgraPixels.Length == 0) return;

        // Check for resolution change
        if (frameWidth != _width || frameHeight != _height)
        {
            Resize(frameWidth, frameHeight);
        }

        // If previous frame render is still in progress, drop to prevent UI lag and maintain lowest latency
        lock (_lock)
        {
            if (_isRendering) return;
            _isRendering = true;
        }

        _dispatcher.BeginInvoke(DispatcherPriority.Render, () =>
        {
            try
            {
                if (_bitmap == null || _bitmap.PixelWidth != frameWidth || _bitmap.PixelHeight != frameHeight)
                {
                    return;
                }

                _bitmap.Lock();
                try
                {
                    unsafe
                    {
                        fixed (byte* pSrc = bgraPixels)
                        {
                            byte* pDst = (byte*)_bitmap.BackBuffer;
                            int bytesToCopy = Math.Min(bgraPixels.Length, _bitmap.BackBufferStride * frameHeight);
                            Buffer.MemoryCopy(pSrc, pDst, bytesToCopy, bytesToCopy);
                        }
                    }

                    _bitmap.AddDirtyRect(new Int32Rect(0, 0, frameWidth, frameHeight));
                }
                finally
                {
                    _bitmap.Unlock();
                }

                RenderFpsTracker.RecordFrame();
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Error occurred during frame rendering");
            }
            finally
            {
                lock (_lock)
                {
                    _isRendering = false;
                }
            }
        });
    }

    /// <summary>
    /// Clears the render surface back to dark solid color.
    /// </summary>
    public void Clear(Color clearColor)
    {
        _dispatcher.BeginInvoke(DispatcherPriority.Render, () =>
        {
            if (_bitmap == null) return;

            try
            {
                _bitmap.Lock();
                try
                {
                    unsafe
                    {
                        uint* pBuffer = (uint*)_bitmap.BackBuffer;
                        uint colorVal = (uint)((clearColor.A << 24) | (clearColor.R << 16) | (clearColor.G << 8) | clearColor.B);
                        int count = _bitmap.PixelWidth * _bitmap.PixelHeight;
                        for (int i = 0; i < count; i++)
                        {
                            pBuffer[i] = colorVal;
                        }
                    }
                    _bitmap.AddDirtyRect(new Int32Rect(0, 0, _bitmap.PixelWidth, _bitmap.PixelHeight));
                }
                finally
                {
                    _bitmap.Unlock();
                }
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Error clearing frame surface");
            }
        });
    }
}
