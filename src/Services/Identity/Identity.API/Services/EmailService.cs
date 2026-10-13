using System.Net;
using System.Net.Mail;

namespace Identity.API.Services;

public class EmailService(ILogger<EmailService> logger, IConfiguration configuration) : IEmailService
{
    public async Task SendOtpEmailAsync(string toEmail, string otpCode, string recipientName)
    {
        // 1. Luôn log rõ ràng ra console để dev/tester thấy ngay lập tức
        logger.LogInformation("=================================================");
        logger.LogInformation(" [EMAIL SERVICE] OTP DISPATCHED");
        logger.LogInformation(" Recipient: {Recipient} <{Email}>", recipientName, toEmail);
        logger.LogInformation(" OTP Code:  >>> {OtpCode} <<< (Expires in 5 minutes)", otpCode);
        logger.LogInformation("=================================================");

        // 2. Nếu có cấu hình SMTP thực tế thì gửi qua SmtpClient
        var smtpHost = configuration["EmailSettings:SmtpHost"];
        if (!string.IsNullOrEmpty(smtpHost))
        {
            try
            {
                var smtpPort = int.Parse(configuration["EmailSettings:SmtpPort"] ?? "587");
                var smtpUser = configuration["EmailSettings:SmtpUser"];
                var smtpPass = configuration["EmailSettings:SmtpPass"];
                var fromEmail = configuration["EmailSettings:FromEmail"] ?? "no-reply@acadprep.com";

                using var client = new SmtpClient(smtpHost, smtpPort)
                {
                    EnableSsl = true,
                    Credentials = !string.IsNullOrEmpty(smtpUser) ? new NetworkCredential(smtpUser, smtpPass) : null
                };

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(fromEmail, "AcadPrep Platform"),
                    Subject = $"[{otpCode}] Mã xác thực tài khoản AcadPrep của bạn",
                    Body = $@"
                        <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #e0e0e0; border-radius: 8px;'>
                            <h2 style='color: #2563eb;'>AcadPrep — Xác thực tài khoản</h2>
                            <p>Xin chào <strong>{recipientName}</strong>,</p>
                            <p>Bạn vừa thực hiện thao tác yêu cầu mã xác thực OTP trên nền tảng AcadPrep. Mã của bạn là:</p>
                            <div style='text-align: center; margin: 24px 0;'>
                                <span style='font-size: 32px; font-weight: bold; letter-spacing: 6px; color: #1e40af; background: #eff6ff; padding: 12px 24px; border-radius: 6px; border: 1px dashed #3b82f6;'>{otpCode}</span>
                            </div>
                            <p style='color: #64748b; font-size: 14px;'>Mã này có hiệu lực trong vòng <strong>5 phút</strong>. Tuyệt đối không chia sẻ mã này cho bất kỳ ai.</p>
                            <hr style='border: none; border-top: 1px solid #f1f5f9; margin: 20px 0;' />
                            <p style='font-size: 12px; color: #94a3b8;'>Đội ngũ phát triển AcadPrep</p>
                        </div>",
                    IsBodyHtml = true
                };
                mailMessage.To.Add(toEmail);

                await client.SendMailAsync(mailMessage);
                logger.LogInformation("Email OTP sent successfully via SMTP to {Email}", toEmail);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to send OTP email via SMTP to {Email}. Fallback to console log.", toEmail);
            }
        }
    }
}
