using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SnipLink.Api.Options;

namespace SnipLink.Api.Services;

public sealed class ResendEmailService : IEmailService
{
    private readonly ResendOptions _resend;
    private readonly EmailOptions _email;
    private readonly HttpClient _http;
    private readonly ILogger<ResendEmailService> _logger;

    private static readonly JsonSerializerOptions _json =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private record ResendRequest(string From, string[] To, string Subject, string Html);

    public ResendEmailService(
        IOptions<ResendOptions> resend,
        IOptions<EmailOptions> email,
        HttpClient http,
        ILogger<ResendEmailService> logger)
    {
        _resend = resend.Value;
        _email  = email.Value;
        _http   = http;
        _logger = logger;
    }

    public async Task SendAsync(string to, string subject, string htmlBody)
    {
        var from = string.IsNullOrWhiteSpace(_email.FromName)
            ? _email.From
            : $"{_email.FromName} <{_email.From}>";

        var payload = new ResendRequest(from, [to], subject, htmlBody);
        var json    = JsonSerializer.Serialize(payload, _json);

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _resend.ApiKey);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _http.SendAsync(request);
        var body     = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "Resend API error {Status} sending to {To}: {Body}",
                (int)response.StatusCode, to, body);
            response.EnsureSuccessStatusCode();
        }

        using var doc = JsonDocument.Parse(body);
        var messageId = doc.RootElement.TryGetProperty("id", out var id) ? id.GetString() : "?";
        _logger.LogInformation(
            "Email sent via Resend to {To}: {Subject} (id: {MessageId})", to, subject, messageId);
    }
}
