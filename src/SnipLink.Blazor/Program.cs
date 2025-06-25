using Radzen;
using SnipLink.Blazor.Components;
using SnipLink.Blazor.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddRadzenComponents();

var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "https://localhost:7288";
builder.Services.AddHttpClient("sniplink_api", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
}).ConfigurePrimaryHttpMessageHandler(() =>
{
    var handler = new HttpClientHandler
    {
        AllowAutoRedirect = false,
        UseCookies = false
    };
    // Dev cert is self-signed and not trusted on Linux; bypass validation locally only.
    if (builder.Environment.IsDevelopment())
        handler.ServerCertificateCustomValidationCallback =
            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
    return handler;
});

builder.Services.AddScoped<SnipLinkApiClient>();
builder.Services.AddScoped<AuthStateService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
