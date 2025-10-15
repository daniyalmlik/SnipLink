using SnipLink.Api.Services;

namespace SnipLink.Tests.Unit;

[Trait("Category", "Unit")]
public sealed class SlugGeneratorTests
{
    private readonly SlugGenerator _sut = new();

    [Theory]
    [InlineData(3)]
    [InlineData(7)]
    [InlineData(20)]
    [InlineData(50)]
    public void Generate_ReturnsSlugOfRequestedLength(int length)
    {
        var slug = _sut.Generate(length);
        Assert.Equal(length, slug.Length);
    }

    [Fact]
    public void Generate_ProducesUniqueSlugs()
    {
        var slugs = Enumerable.Range(0, 100).Select(_ => _sut.Generate()).ToHashSet();
        Assert.True(slugs.Count >= 95, $"Expected ≥95 unique slugs, got {slugs.Count}");
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("me")]
    [InlineData("abc")]
    [InlineData("my-link")]
    [InlineData("a1b2c3")]
    public void IsValid_AcceptsValidSlugs(string slug)
    {
        Assert.True(_sut.IsValid(slug));
    }

    [Theory]
    [InlineData("a")]        // too short
    [InlineData("")]         // empty
    [InlineData("-abc")]     // starts with hyphen
    [InlineData("abc-")]     // ends with hyphen
    [InlineData("ABC")]      // uppercase
    [InlineData("ab cd")]    // space
    [InlineData("ab!cd")]    // special char
    public void IsValid_RejectsInvalidSlugs(string slug)
    {
        Assert.False(_sut.IsValid(slug));
    }
}
