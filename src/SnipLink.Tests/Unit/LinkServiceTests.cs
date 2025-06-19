using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using SnipLink.Api.Data;
using SnipLink.Api.Services;
using SnipLink.Shared.Common;
using SnipLink.Shared.DTOs;

namespace SnipLink.Tests.Unit;

[Trait("Category", "Unit")]
public sealed class LinkServiceTests : IDisposable
{
    // SQLite in-memory is used instead of EF InMemory because LinkService uses
    // ExecuteDeleteAsync (bulk delete), which EF InMemory does not support.
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly IAbuseDetectionService _abuse;
    private readonly LinkService _sut;

    private const string UserId      = "user-1";
    private const string OtherUserId = "user-2";
    private const string BaseUrl     = "https://snip.test";

    public LinkServiceTests()
    {
        // Keep the connection open so the in-memory database persists for the test lifetime.
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        // Disable FK enforcement: unit tests use fake OwnerId strings without
        // a corresponding AspNetUsers row, so FK constraints would fail.
        using var pragma = _connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = OFF";
        pragma.ExecuteNonQuery();

        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new SqliteTestDbContext(opts);
        _db.Database.EnsureCreated();

        _abuse = Substitute.For<IAbuseDetectionService>();
        _abuse.IsUrlSafe(Arg.Any<string>()).Returns(true);
        _abuse.IsSlugBlockedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);

        _sut = new LinkService(_db, new SlugGenerator(), _abuse);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    // ── CreateAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_WithValidUrl_ReturnsSuccessWithNonEmptySlug()
    {
        var result = await _sut.CreateAsync(
            new CreateLinkRequest { OriginalUrl = "https://example.com" }, UserId, BaseUrl);

        var success = Assert.IsType<ServiceResult<LinkResponse>.Success>(result);
        Assert.NotEmpty(success.Value.Slug);
        Assert.Equal("https://example.com", success.Value.OriginalUrl);
    }

    [Fact]
    public async Task CreateAsync_WithCustomSlug_UsesProvidedSlug()
    {
        var result = await _sut.CreateAsync(
            new CreateLinkRequest { OriginalUrl = "https://example.com", Slug = "my-link" },
            UserId, BaseUrl);

        var success = Assert.IsType<ServiceResult<LinkResponse>.Success>(result);
        Assert.Equal("my-link", success.Value.Slug);
    }

    [Fact]
    public async Task CreateAsync_WithDuplicateSlug_ReturnsConflict()
    {
        var request = new CreateLinkRequest { OriginalUrl = "https://example.com", Slug = "taken" };
        await _sut.CreateAsync(request, UserId, BaseUrl);

        var result = await _sut.CreateAsync(request, UserId, BaseUrl);

        Assert.IsType<ServiceResult<LinkResponse>.Conflict>(result);
    }

    [Theory]
    [InlineData("ftp://example.com")]
    [InlineData("not-a-url")]
    [InlineData("example.com")]
    public async Task CreateAsync_WithInvalidUrl_ReturnsInvalid(string url)
    {
        var result = await _sut.CreateAsync(
            new CreateLinkRequest { OriginalUrl = url }, UserId, BaseUrl);

        Assert.IsType<ServiceResult<LinkResponse>.Invalid>(result);
    }

    // ── DeleteAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_OwnLink_ReturnsTrue()
    {
        var created = (ServiceResult<LinkResponse>.Success)
            await _sut.CreateAsync(
                new CreateLinkRequest { OriginalUrl = "https://example.com" }, UserId, BaseUrl);

        var deleted = await _sut.DeleteAsync(created.Value.Id, UserId);

        Assert.True(deleted);
    }

    [Fact]
    public async Task DeleteAsync_OtherUserLink_ReturnsFalse()
    {
        var created = (ServiceResult<LinkResponse>.Success)
            await _sut.CreateAsync(
                new CreateLinkRequest { OriginalUrl = "https://example.com" }, UserId, BaseUrl);

        var deleted = await _sut.DeleteAsync(created.Value.Id, OtherUserId);

        Assert.False(deleted);
    }
}
