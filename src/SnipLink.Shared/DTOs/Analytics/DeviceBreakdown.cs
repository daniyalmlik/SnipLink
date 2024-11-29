namespace SnipLink.Shared.DTOs.Analytics;

public sealed class DeviceBreakdown
{
    public string DeviceType { get; init; } = string.Empty;
    public long Clicks { get; init; }
    public double Percentage { get; init; }
}
