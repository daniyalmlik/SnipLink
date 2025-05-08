using System.Net;
using System.Net.Http.Json;
using SnipLink.Shared.DTOs;
using SnipLink.Shared.DTOs.Analytics;

namespace SnipLink.Blazor.Services;

/// <summary>
/// Scoped typed client wrapping all SnipLink API calls.
/// Manages the auth cookie in-memory for the duration of the Blazor circuit.
/// </summary>
public class SnipLinkApiClient
{
    private readonly HttpClient _http;
    private string? _authCookieHeader;

    public SnipLinkApiClient(IHttpClientFactory factory)
    {
        _http = factory.CreateClient("sniplink_api");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private HttpRequestMessage NewRequest(HttpMethod method, string url)
    {
        var req = new HttpRequestMessage(method, url);
        if (_authCookieHeader is not null)
            req.Headers.TryAddWithoutValidation("Cookie", _authCookieHeader);
        return req;
    }

    /// <summary>
    /// Captures the auth cookie(s) from Set-Cookie headers on login/register responses
    /// so subsequent requests remain authenticated for this circuit.
    /// </summary>
    private void CaptureCookies(HttpResponseMessage response)
    {
        if (response.Headers.TryGetValues("Set-Cookie", out var values))
        {
            var pairs = values
                .Select(v => v.Split(';')[0].Trim())
                .Where(p => p.Contains('='));
            _authCookieHeader = string.Join("; ", pairs);
        }
    }

    public void ClearCookies() => _authCookieHeader = null;

    // ── Auth ─────────────────────────────────────────────────────────────────

    public async Task<(AuthResponse? Result, string? Error)> RegisterAsync(RegisterRequest request)
    {
        var req = NewRequest(HttpMethod.Post, "api/auth/register");
        req.Content = JsonContent.Create(request);
        HttpResponseMessage resp;
        try { resp = await _http.SendAsync(req); }
        catch { return (null, "Could not reach the server."); }

        if (!resp.IsSuccessStatusCode)
            return (null, await ReadErrorAsync(resp) ?? "Registration failed.");

        CaptureCookies(resp);
        return (await resp.Content.ReadFromJsonAsync<AuthResponse>(), null);
    }

    public async Task<(AuthResponse? Result, string? Error)> LoginAsync(LoginRequest request)
    {
        var req = NewRequest(HttpMethod.Post, "api/auth/login");
        req.Content = JsonContent.Create(request);
        HttpResponseMessage resp;
        try { resp = await _http.SendAsync(req); }
        catch { return (null, "Could not reach the server."); }

        if (!resp.IsSuccessStatusCode)
        {
            var error = resp.StatusCode == HttpStatusCode.Unauthorized
                ? "Invalid email or password."
                : await ReadErrorAsync(resp) ?? "Login failed.";
            return (null, error);
        }

        CaptureCookies(resp);
        return (await resp.Content.ReadFromJsonAsync<AuthResponse>(), null);
    }

    public async Task LogoutAsync()
    {
        var req = NewRequest(HttpMethod.Post, "api/auth/logout");
        try { await _http.SendAsync(req); } catch { }
        ClearCookies();
    }

    public async Task<UserInfo?> GetCurrentUserAsync()
    {
        var req = NewRequest(HttpMethod.Get, "api/auth/me");
        try
        {
            var resp = await _http.SendAsync(req);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<UserInfo>();
        }
        catch { return null; }
    }

    // ── Links ─────────────────────────────────────────────────────────────────

    public async Task<(LinkResponse? Link, string? Error)> CreateLinkAsync(CreateLinkRequest request)
    {
        var req = NewRequest(HttpMethod.Post, "api/links");
        req.Content = JsonContent.Create(request);
        HttpResponseMessage resp;
        try { resp = await _http.SendAsync(req); }
        catch { return (null, "Could not reach the server."); }

        if (!resp.IsSuccessStatusCode)
        {
            var error = resp.StatusCode switch
            {
                HttpStatusCode.Conflict => "That slug is already in use.",
                HttpStatusCode.BadRequest => "Invalid request. Check the URL and try again.",
                _ => "Failed to create link."
            };
            return (null, error);
        }

        return (await resp.Content.ReadFromJsonAsync<LinkResponse>(), null);
    }

    public async Task<LinkListResponse?> GetLinksAsync(int page = 1, int pageSize = 20, string? search = null)
    {
        var url = $"api/links?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(search))
            url += $"&search={Uri.EscapeDataString(search)}";

        var req = NewRequest(HttpMethod.Get, url);
        try
        {
            var resp = await _http.SendAsync(req);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<LinkListResponse>();
        }
        catch { return null; }
    }

    public async Task<LinkResponse?> GetLinkAsync(Guid id)
    {
        var req = NewRequest(HttpMethod.Get, $"api/links/{id}");
        try
        {
            var resp = await _http.SendAsync(req);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<LinkResponse>();
        }
        catch { return null; }
    }

    public async Task<bool> DeleteLinkAsync(Guid id)
    {
        var req = NewRequest(HttpMethod.Delete, $"api/links/{id}");
        try
        {
            var resp = await _http.SendAsync(req);
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<LinkResponse?> ToggleLinkAsync(Guid id)
    {
        var req = NewRequest(HttpMethod.Patch, $"api/links/{id}/toggle");
        try
        {
            var resp = await _http.SendAsync(req);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<LinkResponse>();
        }
        catch { return null; }
    }

    // ── Analytics ────────────────────────────────────────────────────────────

    public async Task<AnalyticsSummary?> GetAnalyticsAsync(Guid id, int days = 30)
    {
        var req = NewRequest(HttpMethod.Get, $"api/links/{id}/analytics?days={days}");
        try
        {
            var resp = await _http.SendAsync(req);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<AnalyticsSummary>();
        }
        catch { return null; }
    }

    public async Task<DashboardSummary?> GetDashboardAsync()
    {
        var req = NewRequest(HttpMethod.Get, "api/dashboard");
        try
        {
            var resp = await _http.SendAsync(req);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<DashboardSummary>();
        }
        catch { return null; }
    }

    public async Task<QrCodeResponse?> GetQrCodeAsync(Guid id)
    {
        var req = NewRequest(HttpMethod.Get, $"api/links/{id}/qr");
        try
        {
            var resp = await _http.SendAsync(req);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<QrCodeResponse>();
        }
        catch { return null; }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static async Task<string?> ReadErrorAsync(HttpResponseMessage response)
    {
        try
        {
            var body = await response.Content.ReadAsStringAsync();
            return string.IsNullOrWhiteSpace(body) ? null : body;
        }
        catch { return null; }
    }
}
