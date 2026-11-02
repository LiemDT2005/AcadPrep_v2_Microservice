namespace Identity.API.IServices;

public interface IEmailService
{
    Task SendOtpEmailAsync(string toEmail, string otpCode, string recipientName);
}
