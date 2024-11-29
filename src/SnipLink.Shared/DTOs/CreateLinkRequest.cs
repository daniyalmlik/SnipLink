using System.ComponentModel.DataAnnotations;

namespace SnipLink.Shared.DTOs;

public sealed class CreateLinkRequest
{
    [Required]
    [MaxLength(2048)]
    public string OriginalUrl { get; init; } = string.Empty;

    /// <summary>Optional custom slug. When omitted, a random 7-char slug is generated.</summary>
    [MaxLength(50)]
    public string? Slug { get; init; }

    [MaxLength(256)]
    public string? Title { get; init; }

    public DateTime? ExpiresAt { get; init; }
}
