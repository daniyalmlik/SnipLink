using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SnipLink.Api.Data;
using SnipLink.Api.Domain;
using SnipLink.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Database ──────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sql => sql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)
    ));

// ── Identity ──────────────────────────────────────────────────────────────────
builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequireDigit            = true;
        options.Password.RequireLowercase        = true;
        options.Password.RequireUppercase        = true;
        options.Password.RequireNonAlphanumeric  = true;
        options.Password.RequiredLength          = 12;

        // 5 failed attempts → 15-minute account lockout
        options.Lockout.MaxFailedAccessAttempts  = 5;
        options.Lockout.DefaultLockoutTimeSpan   = TimeSpan.FromMinutes(15);
        options.Lockout.AllowedForNewUsers       = true;

        options.User.RequireUniqueEmail          = true;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

// ── Cookie auth — HttpOnly, Secure, SameSite=Strict ──────────────────────────
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly    = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite   = SameSiteMode.Strict;
    options.Cookie.Name       = "sniplink.auth";
    options.ExpireTimeSpan    = TimeSpan.FromDays(7);
    options.SlidingExpiration = true;
    options.LoginPath         = "/api/auth/login";
    options.LogoutPath        = "/api/auth/logout";
    options.AccessDeniedPath  = "/api/auth/forbidden";

    // Return 401 JSON instead of redirecting API clients
    options.Events.OnRedirectToLogin = ctx =>
    {
        ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = ctx =>
    {
        ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    };
});

// ── Rate limiting ─────────────────────────────────────────────────────────────
builder.Services.AddRateLimiter(options =>
{
    // Public redirect: 60 req/min per IP (sliding window)
    options.AddSlidingWindowLimiter("Redirect", opt =>
    {
        opt.PermitLimit          = 60;
        opt.Window               = TimeSpan.FromMinutes(1);
        opt.SegmentsPerWindow    = 6;
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit           = 0;
    });

    // Authenticated link creation: 20 req/min per user
    options.AddSlidingWindowLimiter("CreateLink", opt =>
    {
        opt.PermitLimit          = 20;
        opt.Window               = TimeSpan.FromMinutes(1);
        opt.SegmentsPerWindow    = 6;
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit           = 0;
    });

    // Analytics + dashboard reads: 30 req/min
    options.AddSlidingWindowLimiter("Analytics", opt =>
    {
        opt.PermitLimit          = 30;
        opt.Window               = TimeSpan.FromMinutes(1);
        opt.SegmentsPerWindow    = 6;
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit           = 0;
    });

    options.OnRejected = async (ctx, ct) =>
    {
        ctx.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        ctx.HttpContext.Response.ContentType = "application/json";
        await ctx.HttpContext.Response.WriteAsync(
            """{"error":"Too many requests. Please slow down."}""", ct);
    };
});

// ── Application services ──────────────────────────────────────────────────────
builder.Services.AddScoped<ISlugGenerator, SlugGenerator>();
builder.Services.AddScoped<IAbuseDetectionService, AbuseDetectionService>();
builder.Services.AddScoped<ILinkService, LinkService>();
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();
builder.Services.AddSingleton<IQrCodeService, QrCodeService>();

// Click tracking: singleton queue + background worker
builder.Services.AddSingleton<ClickTrackingQueue>();
builder.Services.AddSingleton<IClickTrackingQueue>(sp =>
    sp.GetRequiredService<ClickTrackingQueue>());
builder.Services.AddHostedService<ClickTrackingWorker>();

builder.Services.AddControllers();

// ── Build ─────────────────────────────────────────────────────────────────────
var app = builder.Build();

app.UseHttpsRedirection();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

// Expose Program for WebApplicationFactory in integration tests
public partial class Program { }
