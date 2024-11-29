using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SnipLink.Api.Domain;
using SnipLink.Shared.DTOs;

namespace SnipLink.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signIn;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signIn)
    {
        _userManager = userManager;
        _signIn = signIn;
    }

    // ── Register ──────────────────────────────────────────────────────────────

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var user = new ApplicationUser
        {
            UserName    = request.Email,
            Email       = request.Email,
            DisplayName = request.DisplayName
        };

        var result = await _userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
        {
            var errors = result.Errors.Select(e => e.Description);
            return BadRequest(new { errors });
        }

        await _signIn.SignInAsync(user, isPersistent: false);

        return Ok(BuildAuthResponse(user));
    }

    // ── Login ─────────────────────────────────────────────────────────────────

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await _signIn.PasswordSignInAsync(
            userName: request.Email,
            password: request.Password,
            isPersistent: false,
            lockoutOnFailure: true);

        if (result.IsLockedOut)
            return StatusCode(StatusCodes.Status429TooManyRequests,
                new { error = "Account is temporarily locked. Please try again later." });

        if (!result.Succeeded)
            return Unauthorized(new { error = "Invalid email or password." });

        var user = await _userManager.FindByEmailAsync(request.Email);
        return Ok(BuildAuthResponse(user!));
    }

    // ── Logout ────────────────────────────────────────────────────────────────

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await _signIn.SignOutAsync();
        return NoContent();
    }

    // ── Me ────────────────────────────────────────────────────────────────────

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return Unauthorized();

        return Ok(BuildAuthResponse(user).User);
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private static AuthResponse BuildAuthResponse(ApplicationUser user) => new()
    {
        User = new UserInfo
        {
            Id          = user.Id,
            Email       = user.Email!,
            DisplayName = user.DisplayName
        }
    };
}
