namespace SnipLink.Shared.DTOs;

public sealed class QrCodeResponse
{
    public string Slug { get; init; } = string.Empty;
    public string ShortUrl { get; init; } = string.Empty;

    /// <summary>Base64-encoded PNG image of the QR code.</summary>
    public string PngBase64 { get; init; } = string.Empty;
}
