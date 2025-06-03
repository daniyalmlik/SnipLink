using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SnipLink.Api.Data;
using SnipLink.Api.Domain;
using SnipLink.Api.Middleware;
using SnipLink.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Database ──────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(
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
    options.Cookie.HttpOnly     = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite     = SameSiteMode.Strict;
    options.Cookie.Name         = "sniplink.auth";
    options.ExpireTimeSpan      = TimeSpan.FromDays(7);
    options.SlidingExpiration   = true;
    options.LoginPath           = "/api/auth/login";
    options.LogoutPath          = "/api/auth/logout";
    options.AccessDeniedPath    = "/api/auth/forbidden";

    // Return 401/403 JSON instead of redirecting API clients
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
    // Authenticated link creation: token bucket — burst of 20, refill 10/min, queue 5
    options.AddTokenBucketLimiter("CreateLink", opt =>
    {
        opt.TokenLimit            = 20;
        opt.ReplenishmentPeriod   = TimeSpan.FromMinutes(1);
        opt.TokensPerPeriod       = 10;
        opt.AutoReplenishment     = true;
        opt.QueueProcessingOrder  = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit            = 5;
    });

    // Public redirect: fixed window — 100 req/s, no queue (drop immediately)
    options.AddFixedWindowLimiter("Redirect", opt =>
    {
        opt.PermitLimit          = 100;
        opt.Window               = TimeSpan.FromSeconds(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit           = 0;
    });

    // Analytics + dashboard reads: fixed window — 60 req/min, queue 5
    options.AddFixedWindowLimiter("Analytics", opt =>
    {
        opt.PermitLimit          = 60;
        opt.Window               = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit           = 5;
    });

    options.OnRejected = async (ctx, ct) =>
    {
        ctx.HttpContext.Response.StatusCode  = StatusCodes.Status429TooManyRequests;
        ctx.HttpContext.Response.ContentType = "application/json";
        await ctx.HttpContext.Response.WriteAsync(
            """{"error":"Too many requests. Please slow down."}""", ct);
    };
});

// ── CORS ──────────────────────────────────────────────────────────────────────
var allowedOrigin = builder.Configuration["Cors:AllowedOrigin"]
    ?? throw new InvalidOperationException("Cors:AllowedOrigin is not configured.");

builder.Services.AddCors(options =>
    options.AddPolicy("BlazorClient", policy =>
        policy.WithOrigins(allowedOrigin)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()));

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

// ── Health checks ─────────────────────────────────────────────────────────────
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>(name: "db", tags: ["db", "sqlite"]);

// ── Swagger (development only) ────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new() { Title = "SnipLink API", Version = "v1" });
    // Allow sending the auth cookie from Swagger UI
    options.AddSecurityDefinition("cookieAuth", new()
    {
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        In   = Microsoft.OpenApi.Models.ParameterLocation.Cookie,
        Name = "sniplink.auth"
    });
    options.AddSecurityRequirement(new()
    {
        [new() { Reference = new() { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "cookieAuth" } }] = []
    });
});

builder.Services.AddControllers();

// ── Build ─────────────────────────────────────────────────────────────────────
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "SnipLink v1"));
}

app.UseHttpsRedirection();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseCors("BlazorClient");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecks("/healthz");
app.MapControllers();

app.Run();

// Expose Program for WebApplicationFactory in integration tests
public partial class Program { }
