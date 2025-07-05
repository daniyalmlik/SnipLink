using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using SnipLink.Api.Options;

namespace SnipLink.Api.Services;

public sealed class SmtpEmailService : IEmailService
{
    private readonly EmailOptions _options;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(IOptions<EmailOptions> options, ILogger<SmtpEmailService> logger)
    {
        _options = options.Value;
        _logger  = logger;
    }

    public async Task SendAsync(string to, string subject, string htmlBody)
    {
        // In development or when no SMTP host is configured, log to console instead of sending.
        if (string.IsNullOrWhiteSpace(_options.Host))
        {
            _logger.LogInformation(
                "[EMAIL - no SMTP configured]\nTo: {To}\nSubject: {Subject}\nBody:\n{Body}",
                to, subject, htmlBody);
            return;
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_options.FromName, _options.From));
        message.To.Add(new MailboxAddress(string.Empty, to));
        message.Subject = subject;
        message.Body    = new TextPart("html") { Text = htmlBody };

        using var client = new SmtpClient();
        await client.ConnectAsync(
            _options.Host,
            _options.Port,
            _options.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable);

        if (!string.IsNullOrWhiteSpace(_options.Username))
            await client.AuthenticateAsync(_options.Username, _options.Password);

        await client.SendAsync(message);
        await client.DisconnectAsync(quit: true);

        _logger.LogInformation("Email sent to {To}: {Subject}", to, subject);
    }
}
