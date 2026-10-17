using System.IO;
using System.Windows.Media.Imaging;
using QRCoder;
using Serilog;

namespace MirrorCast.Receiver.Utils;

/// <summary>
/// Utility for generating high-contrast QR codes rendered directly as WPF BitmapSource.
/// </summary>
public static class QrCodeGenerator
{
    private static readonly ILogger Logger = Log.ForContext(typeof(QrCodeGenerator));

    /// <summary>
    /// Generates a WPF-compatible BitmapSource containing the QR code for the provided payload string.
    /// </summary>
    /// <param name="payload">The text or URI payload to encode.</param>
    /// <param name="pixelsPerModule">Module pixel density (default 8 for crisp scaling).</param>
    /// <returns>A frozen BitmapSource ready for WPF UI binding.</returns>
    public static BitmapSource GenerateQrCodeImage(string payload, int pixelsPerModule = 8)
    {
        try
        {
            using var qrGenerator = new QRCodeGenerator();
            using QRCodeData qrCodeData = qrGenerator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.M);
            var qrCode = new PngByteQRCode(qrCodeData);
            byte[] qrCodeBytes = qrCode.GetGraphic(pixelsPerModule);

            var bitmapImage = new BitmapImage();
            using (var stream = new MemoryStream(qrCodeBytes))
            {
                bitmapImage.BeginInit();
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.StreamSource = stream;
                bitmapImage.EndInit();
            }

            bitmapImage.Freeze(); // Make cross-thread safe
            return bitmapImage;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to generate QR code for payload: {Payload}", payload);
            // Return an empty transparent 1x1 image on error
            return BitmapSource.Create(1, 1, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null, new byte[4], 4);
        }
    }
}
