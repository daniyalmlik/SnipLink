using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using SnipLink.Shared.DTOs;

namespace SnipLink.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class ApiIntegrationTests : IClassFixture<SnipLinkWebAppFactory>
{
    private readonly SnipLinkWebAppFactory _factory;

    public ApiIntegrationTests(SnipLinkWebAppFactory factory)
    {
        _factory = factory;
    }

    // ── Auth ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Register_ValidRequest_ReturnsOkWithUser()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Email       = $"reg-{Guid.NewGuid()}@example.com",
            Password    = "Test@Password1!",
            DisplayName = "Test User"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(body?.User);
        Assert.NotEmpty(body.User.Email);
    }

    // ── Links ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateLink_WithoutAuth_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/links",
            new CreateLinkRequest { OriginalUrl = "https://example.com" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateLink_WithAuth_Returns201WithSlug()
    {
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/links",
            new CreateLinkRequest { OriginalUrl = "https://example.com" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<LinkResponse>();
        Assert.NotNull(body);
        Assert.NotEmpty(body.Slug);
        Assert.Equal("https://example.com", body.OriginalUrl);
    }

    // ── Redirect ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Redirect_ExistingSlug_Returns301ToOriginalUrl()
    {
        // Create a link first
        var authClient = await CreateAuthenticatedClientAsync();
        var createResp = await authClient.PostAsJsonAsync("/api/links",
            new CreateLinkRequest { OriginalUrl = "https://example.com/target" });
        createResp.EnsureSuccessStatusCode();
        var link = await createResp.Content.ReadFromJsonAsync<LinkResponse>();

        // Follow-redirect is off so we can inspect the 301
        var noRedirectClient = _factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await noRedirectClient.GetAsync($"/{link!.Slug}");

        Assert.Equal(HttpStatusCode.MovedPermanently, response.StatusCode);
        Assert.Equal("https://example.com/target", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task Redirect_NonExistentSlug_Returns404()
    {
        var client = _factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/this-slug-does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Registers a fresh user and returns the <see cref="HttpClient"/> with the
    /// resulting auth cookie already stored in its cookie container.
    /// </summary>
    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Email       = $"auth-{Guid.NewGuid()}@example.com",
            Password    = "Test@Password1!",
            DisplayName = "Auth User"
        });
        response.EnsureSuccessStatusCode();

        return client;
    }
}
