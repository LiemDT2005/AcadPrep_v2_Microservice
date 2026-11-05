namespace Notifications.API.Services.Interfaces;

public interface IEmailSender
{
    bool IsConfigured { get; }
    Task SendAsync(string to, string subject, string body, CancellationToken ct);
}
