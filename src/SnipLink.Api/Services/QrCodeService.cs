using QRCoder;

namespace SnipLink.Api.Services;

public sealed class QrCodeService : IQrCodeService
{
    // 20 px per module ≈ 400×400 px for a version-3 QR code — legible on all displays.
    private const int PixelsPerModule = 20;

    public string Generate(string url)
    {
        using var generator = new QRCodeGenerator();
        var data = generator.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);
        using var qr = new PngByteQRCode(data);
        var pngBytes = qr.GetGraphic(PixelsPerModule);
        return Convert.ToBase64String(pngBytes);
    }
}
