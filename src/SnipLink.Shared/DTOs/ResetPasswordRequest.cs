using System.ComponentModel.DataAnnotations;

namespace SnipLink.Shared.DTOs;

public sealed class ResetPasswordRequest
{
    [Required]
    public string UserId { get; init; } = string.Empty;

    [Required]
    public string Token { get; init; } = string.Empty;

    [Required]
    [MinLength(12)]
    [MaxLength(128)]
    public string NewPassword { get; init; } = string.Empty;
}
