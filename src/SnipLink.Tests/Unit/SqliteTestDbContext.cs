using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ValueGeneration;
using SnipLink.Api.Data;
using SnipLink.Api.Domain;

namespace SnipLink.Tests.Unit;

/// <summary>
/// AppDbContext override that replaces SQL Server-specific defaults
/// (NEWID, GETUTCDATE) with SQLite-compatible equivalents so that
/// SQLite in-memory tests can run ExecuteDeleteAsync / ExecuteUpdateAsync.
/// </summary>
internal sealed class SqliteTestDbContext : AppDbContext
{
    public SqliteTestDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Replace NEWID() with a client-side Guid generator
        modelBuilder.Entity<ShortLink>()
            .Property(s => s.Id)
            .HasDefaultValueSql(null)
            .HasValueGenerator<GuidValueGenerator>();

        modelBuilder.Entity<ClickEvent>()
            .Property(c => c.Id)
            .HasDefaultValueSql(null)
            .HasValueGenerator<GuidValueGenerator>();

        // Remove GETUTCDATE() — C# property initializers always provide a value,
        // so EF Core will include it in INSERT statements without a DB default.
        modelBuilder.Entity<ShortLink>()
            .Property(s => s.CreatedAt)
            .HasDefaultValueSql(null);

        modelBuilder.Entity<ClickEvent>()
            .Property(c => c.ClickedAt)
            .HasDefaultValueSql(null);

        modelBuilder.Entity<ApplicationUser>()
            .Property(u => u.CreatedAt)
            .HasDefaultValueSql(null);

        modelBuilder.Entity<BlockedSlug>()
            .Property(b => b.CreatedAt)
            .HasDefaultValueSql(null);
    }
}
