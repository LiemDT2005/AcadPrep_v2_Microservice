using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using Notifications.API.Services.Interfaces;

namespace Notifications.API.Services;

// Configure via user-secrets or environment variables (Smtp__Host, Smtp__Port, Smtp__User, Smtp__Password,
// Smtp__From). Never commit credentials. Notification is the only service that sends email.
public class SmtpOptions
{
    public const string SectionName = "Smtp";
    public string? Host { get; set; }
    public int Port { get; set; } = 587;
    public string? User { get; set; }
    public string? Password { get; set; }
    public string? From { get; set; }
    // Optional display name shown to recipients (e.g. the platform name).
    public string? FromName { get; set; }
    public bool EnableSsl { get; set; } = true;
    public int MaxAttempts { get; set; } = 3;
    public double RetryDelaySeconds { get; set; } = 2;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(From);
}

public class SmtpEmailSender(IOptions<SmtpOptions> options) : IEmailSender
{
    public bool IsConfigured => options.Value.IsConfigured;

    public async Task SendAsync(string to, string subject, string body, CancellationToken ct)
    {
        var settings = options.Value;
        if (!settings.IsConfigured) throw new InvalidOperationException("SMTP is not configured.");
        var from = string.IsNullOrWhiteSpace(settings.FromName)
            ? new MailAddress(settings.From!)
            : new MailAddress(settings.From!, settings.FromName, System.Text.Encoding.UTF8);
        using var message = new MailMessage(from, new MailAddress(to))
        {
            Subject = subject, Body = body, IsBodyHtml = false,
            SubjectEncoding = System.Text.Encoding.UTF8, BodyEncoding = System.Text.Encoding.UTF8
        };
        using var client = new SmtpClient(settings.Host, settings.Port) { EnableSsl = settings.EnableSsl, Timeout = 10_000 };
        if (!string.IsNullOrWhiteSpace(settings.User))
            client.Credentials = new NetworkCredential(settings.User, settings.Password);
        await client.SendMailAsync(message, ct);
    }
}
