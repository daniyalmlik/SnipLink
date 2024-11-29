using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using SnipLink.Api.Data;
using SnipLink.Api.Domain;
using SnipLink.Shared.Enums;

namespace SnipLink.Api.Services;

// ── Data carrier ─────────────────────────────────────────────────────────────

public sealed class ClickTrackingData
{
    public Guid ShortLinkId { get; init; }
    public string? Referrer { get; init; }
    public string? UserAgent { get; init; }
    public string? IpHash { get; init; }
    public string? Country { get; init; }
    public DeviceType DeviceType { get; init; }
}

// ── Queue interface ───────────────────────────────────────────────────────────

public interface IClickTrackingQueue
{
    /// <summary>
    /// Non-blocking enqueue. Drops the oldest event when the buffer is full
    /// rather than blocking the redirect response path.
    /// </summary>
    void Enqueue(ClickTrackingData data);
}

// ── Channel-backed queue (singleton) ─────────────────────────────────────────

public sealed class ClickTrackingQueue : IClickTrackingQueue
{
    // 2 000-event bounded buffer; BoundedChannelFullMode.DropOldest keeps
    // the service responsive under load without allocating unbounded memory.
    private readonly Channel<ClickTrackingData> _channel =
        Channel.CreateBounded<ClickTrackingData>(new BoundedChannelOptions(2_000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });

    public void Enqueue(ClickTrackingData data) =>
        _channel.Writer.TryWrite(data);

    public async IAsyncEnumerable<ClickTrackingData> ReadAllAsync(
        [EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var item in _channel.Reader.ReadAllAsync(ct))
            yield return item;
    }
}

// ── Background worker (hosted service) ───────────────────────────────────────

public sealed class ClickTrackingWorker : BackgroundService
{
    private readonly ClickTrackingQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ClickTrackingWorker> _logger;

    public ClickTrackingWorker(
        IClickTrackingQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<ClickTrackingWorker> logger)
    {
        // Cast is safe: the concrete type is always registered as both
        // IClickTrackingQueue and the concrete type.
        _queue = (ClickTrackingQueue)queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var data in _queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                db.ClickEvents.Add(new ClickEvent
                {
                    ShortLinkId = data.ShortLinkId,
                    Referrer    = data.Referrer,
                    UserAgent   = data.UserAgent,
                    IpHash      = data.IpHash,
                    Country     = data.Country,
                    DeviceType  = data.DeviceType
                });

                // Atomic increment — avoids read-modify-write race.
                await db.ShortLinks
                    .Where(s => s.Id == data.ShortLinkId)
                    .ExecuteUpdateAsync(
                        s => s.SetProperty(l => l.ClickCount, l => l.ClickCount + 1),
                        stoppingToken);

                await db.SaveChangesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to persist click event for ShortLinkId {ShortLinkId}",
                    data.ShortLinkId);
            }
        }
    }
}
