using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SnipLink.Api.Domain;

namespace SnipLink.Api.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<ShortLink> ShortLinks => Set<ShortLink>();
    public DbSet<ClickEvent> ClickEvents => Set<ClickEvent>();
    public DbSet<BlockedSlug> BlockedSlugs => Set<BlockedSlug>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(e =>
        {
            e.Property(u => u.DisplayName)
                .HasMaxLength(100)
                .IsRequired();

            e.Property(u => u.CreatedAt)
                .HasDefaultValueSql("GETUTCDATE()");
        });

        builder.Entity<ShortLink>(e =>
        {
            e.HasKey(s => s.Id);

            e.Property(s => s.Id)
                .HasDefaultValueSql("NEWID()");

            e.Property(s => s.Slug)
                .HasMaxLength(50)
                .IsRequired();

            e.HasIndex(s => s.Slug)
                .IsUnique()
                .HasDatabaseName("IX_ShortLinks_Slug");

            e.Property(s => s.OriginalUrl)
                .HasMaxLength(2048)
                .IsRequired();

            e.Property(s => s.Title)
                .HasMaxLength(256);

            e.Property(s => s.IsActive)
                .HasDefaultValue(true);

            e.Property(s => s.CreatedAt)
                .HasDefaultValueSql("GETUTCDATE()");

            e.Property(s => s.ClickCount)
                .HasDefaultValue(0L);

            e.HasOne(s => s.Owner)
                .WithMany(u => u.Links)
                .HasForeignKey(s => s.OwnerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ClickEvent>(e =>
        {
            e.HasKey(c => c.Id);

            e.Property(c => c.Id)
                .HasDefaultValueSql("NEWID()");

            e.Property(c => c.ClickedAt)
                .HasDefaultValueSql("GETUTCDATE()");

            e.Property(c => c.Referrer)
                .HasMaxLength(2048);

            e.Property(c => c.UserAgent)
                .HasMaxLength(512);

            e.Property(c => c.IpHash)
                .HasMaxLength(64);

            e.Property(c => c.Country)
                .HasMaxLength(2);

            e.Property(c => c.DeviceType)
                .HasConversion<string>()
                .HasMaxLength(10);

            e.HasIndex(c => new { c.ShortLinkId, c.ClickedAt })
                .HasDatabaseName("IX_ClickEvents_ShortLinkId_ClickedAt");

            e.HasOne(c => c.ShortLink)
                .WithMany(s => s.Clicks)
                .HasForeignKey(c => c.ShortLinkId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<BlockedSlug>(e =>
        {
            e.HasKey(b => b.Id);

            e.Property(b => b.Pattern)
                .HasMaxLength(100)
                .IsRequired();

            e.HasIndex(b => b.Pattern)
                .IsUnique()
                .HasDatabaseName("IX_BlockedSlugs_Pattern");

            e.Property(b => b.Reason)
                .HasMaxLength(500)
                .IsRequired();

            e.Property(b => b.CreatedAt)
                .HasDefaultValueSql("GETUTCDATE()");
        });
    }
}
