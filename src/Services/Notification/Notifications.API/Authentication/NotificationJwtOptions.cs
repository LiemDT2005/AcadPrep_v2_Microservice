namespace Notifications.API.Authentication;

public class NotificationJwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string KeyId { get; set; } = string.Empty;
    public string? PublicKeyPath { get; set; }
    public string? PublicKey { get; set; }
}
