using System.ComponentModel.DataAnnotations;

namespace SnipLink.Shared.DTOs;

public sealed class ResendVerificationRequest
{
    [Required]
    [EmailAddress]
    [MaxLength(256)]
    public string Email { get; init; } = string.Empty;
}
