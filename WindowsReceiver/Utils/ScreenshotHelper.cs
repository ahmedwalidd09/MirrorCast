using System.IO;
using System.Windows.Media.Imaging;
using Serilog;

namespace MirrorCast.Receiver.Utils;

/// <summary>
/// Handles capturing and saving high-fidelity screenshots of mirrored video frames.
/// </summary>
public static class ScreenshotHelper
{
    private static readonly ILogger Logger = Log.ForContext(typeof(ScreenshotHelper));

    /// <summary>
    /// Saves the current video frame BitmapSource to the current user's Desktop folder.
    /// </summary>
    /// <param name="frameSource">The active frame image source to encode.</param>
    /// <returns>The full path of the saved PNG screenshot, or null if failed.</returns>
    public static string? SaveFrameScreenshot(BitmapSource? frameSource)
    {
        if (frameSource == null)
        {
            Logger.Warning("Cannot save screenshot: frame source is null");
            return null;
        }

        try
        {
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string fileName = $"MirrorCast_{timestamp}.png";
            string fullPath = Path.Combine(desktopPath, fileName);

            // Clone or freeze if not frozen to ensure thread safety
            BitmapSource sourceToSave = frameSource;
            if (!sourceToSave.IsFrozen && sourceToSave.CanFreeze)
            {
                sourceToSave.Freeze();
            }

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(sourceToSave));

            using (var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                encoder.Save(fileStream);
            }

            Logger.Information("Screenshot saved successfully to {Path}", fullPath);
            return fullPath;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to save frame screenshot to Desktop");
            return null;
        }
    }
}
