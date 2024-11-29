namespace SnipLink.Shared.DTOs.Analytics;

public sealed class ReferrerBreakdown
{
    /// <summary>Extracted hostname, or "direct" when referrer is absent.</summary>
    public string Host { get; init; } = string.Empty;
    public long Clicks { get; init; }
}
