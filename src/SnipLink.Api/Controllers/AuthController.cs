using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using SnipLink.Api.Domain;
using SnipLink.Api.Services;
using SnipLink.Shared.DTOs;

namespace SnipLink.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signIn;
    private readonly IEmailService _email;
    private readonly IConfiguration _config;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signIn,
        IEmailService email,
        IConfiguration config,
        ILogger<AuthController> logger)
    {
        _userManager = userManager;
        _signIn      = signIn;
        _email       = email;
        _config      = config;
        _logger      = logger;
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

        await SendVerificationEmailAsync(user);

        return Ok(new { message = "Registration successful. Please check your email to verify your account." });
    }

    // ── Verify email ──────────────────────────────────────────────────────────

    [HttpGet("verify-email")]
    public async Task<IActionResult> VerifyEmail([FromQuery] string userId, [FromQuery] string token)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(token))
            return BadRequest(new { error = "Invalid verification link." });

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return BadRequest(new { error = "Invalid verification link." });

        string decodedToken;
        try
        {
            decodedToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token));
        }
        catch
        {
            return BadRequest(new { error = "Invalid verification link." });
        }

        var result = await _userManager.ConfirmEmailAsync(user, decodedToken);
        if (!result.Succeeded)
            return BadRequest(new { error = "Email verification failed. The link may have expired." });

        return Ok(new { message = "Email verified successfully. You can now sign in." });
    }

    // ── Resend verification email ─────────────────────────────────────────────

    [HttpPost("resend-verification")]
    public async Task<IActionResult> ResendVerification([FromBody] ResendVerificationRequest request)
    {
        // Always return 200 to prevent user enumeration
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null || await _userManager.IsEmailConfirmedAsync(user))
            return Ok(new { message = "If that address is registered and unverified, a new email has been sent." });

        await SendVerificationEmailAsync(user);

        return Ok(new { message = "If that address is registered and unverified, a new email has been sent." });
    }

    // ── Login ─────────────────────────────────────────────────────────────────

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null)
            return Unauthorized(new { error = "No account found with this email." });

        var result = await _signIn.PasswordSignInAsync(
            userName: request.Email,
            password: request.Password,
            isPersistent: false,
            lockoutOnFailure: true);

        if (result.IsLockedOut)
            return StatusCode(StatusCodes.Status429TooManyRequests,
                new { error = "Account is temporarily locked. Please try again later." });

        if (result.IsNotAllowed)
            return Unauthorized(new { error = "Please verify your email address before signing in." });

        if (!result.Succeeded)
            return Unauthorized(new { error = "Invalid password." });

        return Ok(BuildAuthResponse(user));
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

    // ── Forgot password ───────────────────────────────────────────────────────

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null)
            return NotFound(new { error = "No account found with this email." });

        if (await _userManager.IsEmailConfirmedAsync(user))
            await SendPasswordResetEmailAsync(user);

        return Ok(new { message = "A password reset link has been sent. Check your inbox." });
    }

    // ── Reset password ────────────────────────────────────────────────────────

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        var user = await _userManager.FindByIdAsync(request.UserId);
        if (user is null)
            return BadRequest(new { error = "Invalid or expired password reset link." });

        string decodedToken;
        try
        {
            decodedToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(request.Token));
        }
        catch
        {
            return BadRequest(new { error = "Invalid or expired password reset link." });
        }

        var result = await _userManager.ResetPasswordAsync(user, decodedToken, request.NewPassword);
        if (!result.Succeeded)
        {
            var firstError = result.Errors.FirstOrDefault()?.Description
                ?? "Password reset failed. The link may have expired.";
            return BadRequest(new { error = firstError });
        }

        return Ok(new { message = "Your password has been reset. You can now sign in." });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task SendPasswordResetEmailAsync(ApplicationUser user)
    {
        var rawToken     = await _userManager.GeneratePasswordResetTokenAsync(user);
        var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(rawToken));
        var frontendBase = _config["Frontend:BaseUrl"] ?? "https://localhost:7129";
        var resetUrl     = $"{frontendBase}/reset-password?userId={user.Id}&token={encodedToken}";

        var html = $"""
            <div style="font-family:sans-serif;max-width:600px;margin:0 auto">
              <h2 style="color:#4f46e5">Reset your SnipLink password</h2>
              <p>Hi {user.DisplayName},</p>
              <p>We received a request to reset the password for your account. Click the button below to choose a new password.</p>
              <a href="{resetUrl}"
                 style="display:inline-block;padding:12px 24px;background:#4f46e5;color:#fff;
                        text-decoration:none;border-radius:6px;font-weight:600;margin:16px 0">
                Reset Password
              </a>
              <p style="color:#6b7280;font-size:13px">
                If the button doesn't work, copy and paste this link into your browser:<br/>
                <a href="{resetUrl}">{resetUrl}</a>
              </p>
              <p style="color:#6b7280;font-size:13px">
                This link expires in 24 hours. If you didn't request a password reset, you can safely ignore this email — your password will not be changed.
              </p>
            </div>
            """;

        try
        {
            await _email.SendAsync(user.Email!, "Reset your SnipLink password", html);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to send password reset email to {Email}. Reset URL: {Url}",
                user.Email, resetUrl);
        }
    }

    private async Task SendVerificationEmailAsync(ApplicationUser user)
    {
        var rawToken    = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(rawToken));
        var frontendBase = _config["Frontend:BaseUrl"] ?? "https://localhost:7129";
        var verifyUrl    = $"{frontendBase}/verify-email?userId={user.Id}&token={encodedToken}";

        var html = $"""
            <div style="font-family:sans-serif;max-width:600px;margin:0 auto">
              <h2 style="color:#4f46e5">Welcome to SnipLink, {user.DisplayName}!</h2>
              <p>Thanks for registering. Please verify your email address by clicking the button below.</p>
              <a href="{verifyUrl}"
                 style="display:inline-block;padding:12px 24px;background:#4f46e5;color:#fff;
                        text-decoration:none;border-radius:6px;font-weight:600;margin:16px 0">
                Verify Email Address
              </a>
              <p style="color:#6b7280;font-size:13px">
                If the button doesn't work, copy and paste this link into your browser:<br/>
                <a href="{verifyUrl}">{verifyUrl}</a>
              </p>
              <p style="color:#6b7280;font-size:13px">
                If you didn't create an account, you can safely ignore this email.
              </p>
            </div>
            """;

        try
        {
            await _email.SendAsync(user.Email!, "Verify your SnipLink account", html);
        }
        catch (Exception ex)
        {
            // Don't fail registration if email delivery fails — log the verification link
            // so it's accessible during development.
            _logger.LogError(ex,
                "Failed to send verification email to {Email}. Verification URL: {Url}",
                user.Email, verifyUrl);
        }
    }

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
