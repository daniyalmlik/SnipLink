namespace SnipLink.Shared.DTOs.Analytics;

public sealed class CountryBreakdown
{
    /// <summary>ISO 3166-1 alpha-2 code, or "Unknown" when GeoIP is unavailable.</summary>
    public string Country { get; init; } = string.Empty;
    public long Clicks { get; init; }
}
