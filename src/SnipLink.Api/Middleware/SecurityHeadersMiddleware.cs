namespace SnipLink.Api.Middleware;

public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly bool _isProduction;

    public SecurityHeadersMiddleware(RequestDelegate next, IWebHostEnvironment env)
    {
        _next = next;
        _isProduction = env.IsProduction();
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var h = context.Response.Headers;

        h["X-Content-Type-Options"]  = "nosniff";
        h["X-Frame-Options"]          = "DENY";
        h["X-XSS-Protection"]         = "1; mode=block";
        h["Referrer-Policy"]          = "strict-origin-when-cross-origin";
        h["Permissions-Policy"]       = "camera=(), microphone=(), geolocation=()";
        h["Content-Security-Policy"]  =
            "default-src 'self'; " +
            "script-src 'self'; " +
            "style-src 'self' 'unsafe-inline'; " +
            "img-src 'self' data:; " +
            "frame-ancestors 'none'";

        // HSTS only makes sense over a verified TLS connection in production.
        if (_isProduction)
            h["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";

        await _next(context);
    }
}
