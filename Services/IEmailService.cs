namespace AuthApi.Services;

public interface IEmailService
{
    Task SendPasswordResetEmailAsync(string toEmail, string resetLink);
    Task SendVerificationCodeEmailAsync(string toEmail, string code);
}