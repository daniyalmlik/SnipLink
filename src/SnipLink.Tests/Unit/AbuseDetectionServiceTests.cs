using Microsoft.EntityFrameworkCore;
using SnipLink.Api.Data;
using SnipLink.Api.Services;

namespace SnipLink.Tests.Unit;

[Trait("Category", "Unit")]
public sealed class AbuseDetectionServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly AbuseDetectionService _sut;

    public AbuseDetectionServiceTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(opts);
        _sut = new AbuseDetectionService(_db);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public void IsUrlSafe_AcceptsHttpsUrl()
    {
        Assert.True(_sut.IsUrlSafe("https://example.com"));
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,<h1>xss</h1>")]
    public void IsUrlSafe_RejectsDangerousSchemes(string url)
    {
        Assert.False(_sut.IsUrlSafe(url));
    }

    [Theory]
    [InlineData("https://example.com/malware.exe")]
    [InlineData("https://example.com/script.bat")]
    [InlineData("https://example.com/run.ps1")]
    public void IsUrlSafe_RejectsDangerousExtensions(string url)
    {
        Assert.False(_sut.IsUrlSafe(url));
    }
}
