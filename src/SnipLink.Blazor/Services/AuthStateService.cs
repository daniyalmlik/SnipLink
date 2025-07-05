using SnipLink.Shared.DTOs;

namespace SnipLink.Blazor.Services;

/// <summary>
/// Scoped service holding authentication state for the current Blazor circuit.
/// </summary>
public class AuthStateService
{
    private readonly SnipLinkApiClient _api;

    public UserInfo? CurrentUser { get; private set; }
    public bool IsAuthenticated => CurrentUser is not null;

    /// <summary>Raised whenever auth state changes (login, logout, or initial check).</summary>
    public event Action? OnAuthStateChanged;

    public AuthStateService(SnipLinkApiClient api) => _api = api;

    /// <summary>
    /// Checks the API for an existing session. Call once from MainLayout.OnInitializedAsync.
    /// </summary>
    public async Task InitializeAsync()
    {
        CurrentUser = await _api.GetCurrentUserAsync();
        OnAuthStateChanged?.Invoke();
    }

    /// <summary>Returns null on success, or an error message on failure.</summary>
    public async Task<string?> LoginAsync(LoginRequest request)
    {
        var (response, error) = await _api.LoginAsync(request);
        if (response is null) return error;
        CurrentUser = response.User;
        OnAuthStateChanged?.Invoke();
        return null;
    }

    /// <summary>Returns null on success, or an error message on failure.</summary>
    public async Task<string?> RegisterAsync(RegisterRequest request)
    {
        var (success, error) = await _api.RegisterAsync(request);
        return success ? null : error;
    }

    public async Task LogoutAsync()
    {
        await _api.LogoutAsync();
        CurrentUser = null;
        OnAuthStateChanged?.Invoke();
    }
}
