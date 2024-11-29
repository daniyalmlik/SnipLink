using SnipLink.Shared.Enums;

namespace SnipLink.Api.Domain;

public class ClickEvent
{
    public Guid Id { get; set; }
    public DateTime ClickedAt { get; set; } = DateTime.UtcNow;
    public string? Referrer { get; set; }
    public string? UserAgent { get; set; }
    public string? IpHash { get; set; }
    public string? Country { get; set; }
    public DeviceType DeviceType { get; set; } = DeviceType.Unknown;

    public Guid ShortLinkId { get; set; }
    public ShortLink ShortLink { get; set; } = null!;
}
