namespace Identity.API.Services;

public interface IEmailService
{
    Task SendOtpEmailAsync(string toEmail, string otpCode, string recipientName);
}
