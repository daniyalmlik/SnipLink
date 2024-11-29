namespace SnipLink.Shared.DTOs;

public sealed class LinkResponse
{
    public Guid Id { get; init; }
    public string Slug { get; init; } = string.Empty;
    public string OriginalUrl { get; init; } = string.Empty;
    public string? Title { get; init; }

    /// <summary>Computed absolute short URL, e.g. https://snip.link/abc123</summary>
    public string ShortUrl { get; init; } = string.Empty;

    public bool IsActive { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public long ClickCount { get; init; }
}
