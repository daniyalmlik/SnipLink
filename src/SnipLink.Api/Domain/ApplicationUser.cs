using Microsoft.AspNetCore.Identity;

namespace SnipLink.Api.Domain;

public class ApplicationUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<ShortLink> Links { get; set; } = new List<ShortLink>();
}
