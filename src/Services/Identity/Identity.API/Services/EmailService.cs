using System.Net;
using System.Net.Mail;
using Identity.API.IServices;

namespace Identity.API.Services;

public class EmailService : IEmailService
{
    private readonly ILogger<EmailService> _logger;
    private readonly IConfiguration _configuration;

    public EmailService(ILogger<EmailService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public async Task SendOtpEmailAsync(string toEmail, string otpCode, string recipientName)
    {
        _logger.LogInformation("=================================================");
        _logger.LogInformation(" [EMAIL SERVICE] OTP DISPATCHED");
        _logger.LogInformation(" Recipient: {Recipient} <{Email}>", recipientName, toEmail);
        _logger.LogInformation(" OTP Code:  >>> {OtpCode} <<< (Expires in 5 minutes)", otpCode);
        _logger.LogInformation("=================================================");

        var smtpHost = _configuration["EmailSettings:SmtpHost"];
        if (!string.IsNullOrEmpty(smtpHost))
        {
            try
            {
                var smtpPort = int.Parse(_configuration["EmailSettings:SmtpPort"] ?? "587");
                var smtpUser = _configuration["EmailSettings:SmtpUser"];
                var smtpPass = _configuration["EmailSettings:SmtpPass"];
                var fromEmail = _configuration["EmailSettings:FromEmail"] ?? "no-reply@acadprep.com";

                using var client = new SmtpClient(smtpHost, smtpPort)
                {
                    EnableSsl = true,
                    Credentials = !string.IsNullOrEmpty(smtpUser) ? new NetworkCredential(smtpUser, smtpPass) : null
                };

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(fromEmail, "AcadPrep Platform"),
                    Subject = $"[{otpCode}] Your AcadPrep Verification Code",
                    Body = $@"
                        <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #e0e0e0; border-radius: 8px;'>
                            <h2 style='color: #2563eb;'>AcadPrep — Account Verification</h2>
                            <p>Hello <strong>{recipientName}</strong>,</p>
                            <p>You recently requested an OTP verification code on the AcadPrep platform. Your verification code is:</p>
                            <div style='text-align: center; margin: 24px 0;'>
                                <span style='font-size: 32px; font-weight: bold; letter-spacing: 6px; color: #1e40af; background: #eff6ff; padding: 12px 24px; border-radius: 6px; border: 1px dashed #3b82f6;'>{otpCode}</span>
                            </div>
                            <p style='color: #64748b; font-size: 14px;'>This code is valid for <strong>5 minutes</strong>. Do not share this code with anyone.</p>
                            <hr style='border: none; border-top: 1px solid #f1f5f9; margin: 20px 0;' />
                            <p style='font-size: 12px; color: #94a3b8;'>The AcadPrep Team</p>
                        </div>",
                    IsBodyHtml = true
                };

                mailMessage.To.Add(toEmail);
                await client.SendMailAsync(mailMessage);
                _logger.LogInformation("OTP email sent successfully to {Email} via SMTP.", toEmail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send OTP email to {Email} via SMTP. Fallback to console log.", toEmail);
            }
        }
    }
}
