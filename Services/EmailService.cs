using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace ApimReplica.Services;

public class EmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration config, ILogger<EmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task SendAsync(string subject, string body, CancellationToken ct = default)
    {
        var smtp = _config.GetSection("Smtp");
        var host = smtp["Host"];
        var user = smtp["User"];
        var password = smtp["Password"];
        var to = smtp["To"];

        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(host) ||
            string.IsNullOrEmpty(user) || string.IsNullOrEmpty(to))
        {
            _logger.LogWarning("SMTP not configured. Would send: {Subject}", subject);
            return;
        }

        if (!int.TryParse(smtp["Port"], out var port))
        {
            _logger.LogError("SMTP port '{Port}' is not a number. Not sending: {Subject}", smtp["Port"], subject);
            return;
        }

        // Everything below can throw on malformed config, so it all stays inside the
        // try — an alert must never take down the health check cycle that raised it.
        try
        {
            var message = new MimeMessage();
            message.From.Add(MailboxAddress.Parse(user));
            message.To.Add(MailboxAddress.Parse(to));
            message.Subject = subject;
            message.Body = new TextPart("plain") { Text = body };

            using var client = new SmtpClient();
            await client.ConnectAsync(host, port, SecureSocketOptions.StartTls, ct);
            await client.AuthenticateAsync(user, password, ct);
            await client.SendAsync(message, ct);
            await client.DisconnectAsync(true, ct);

            _logger.LogInformation("Email sent: {Subject}", subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email: {Subject}", subject);
        }
    }
}
