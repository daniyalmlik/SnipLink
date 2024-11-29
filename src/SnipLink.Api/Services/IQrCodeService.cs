namespace SnipLink.Api.Services;

public interface IQrCodeService
{
    /// <summary>
    /// Generates a QR code for <paramref name="url"/> and returns it as a
    /// base64-encoded PNG string suitable for embedding in an img src attribute.
    /// </summary>
    string Generate(string url);
}
