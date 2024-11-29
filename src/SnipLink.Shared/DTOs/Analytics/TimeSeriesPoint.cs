namespace SnipLink.Shared.DTOs.Analytics;

public sealed class TimeSeriesPoint
{
    public DateOnly Date { get; init; }
    public long Clicks { get; init; }
}
