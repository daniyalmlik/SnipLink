namespace SnipLink.Api.Domain;

public class BlockedSlug
{
    public int Id { get; set; }
    public string Pattern { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
