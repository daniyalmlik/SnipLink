using System.ComponentModel.DataAnnotations;

namespace SnipLink.Shared.DTOs;

public sealed class RegisterRequest
{
    [Required]
    [EmailAddress]
    [MaxLength(256)]
    public string Email { get; init; } = string.Empty;

    [Required]
    [MinLength(12)]
    [MaxLength(128)]
    public string Password { get; init; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string DisplayName { get; init; } = string.Empty;
}
