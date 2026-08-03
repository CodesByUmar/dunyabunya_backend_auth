using System.Net;
using System.Net.Mail;

namespace AuthApi.Services;

public class SmtpEmailService : IEmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(IConfiguration config, ILogger<SmtpEmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task SendPasswordResetEmailAsync(string toEmail, string resetLink)
    {
        var host = _config["Smtp:Host"] ?? "smtp.gmail.com";
        var port = int.Parse(_config["Smtp:Port"] ?? "587");
        var username = _config["Smtp:Username"];
        var password = _config["Smtp:Password"];
        var from = _config["Smtp:From"] ?? username;

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            _logger.LogWarning("SMTP sozlamalari to'liq emas. Email yuborilmadi: {Email}", toEmail);
            return;
        }

        using var client = new SmtpClient(host, port)
        {
            EnableSsl = true,
            Credentials = new NetworkCredential(username, password)
        };

        var message = new MailMessage
        {
            From = new MailAddress(from!, "DunyaBunya"),
            Subject = "Parolni tiklash",
            Body = $@"
                <h3>Parolni tiklash so'rovi</h3>
                <p>Parolingizni tiklash uchun quyidagi havolaga bosing (30 daqiqa amal qiladi):</p>
                <p><a href=""{resetLink}"">{resetLink}</a></p>
                <p>Agar bu so'rovni siz yubormagan bo'lsangiz, bu xabarni e'tiborsiz qoldiring.</p>
            ",
            IsBodyHtml = true
        };
        message.To.Add(toEmail);

        try
        {
            await client.SendMailAsync(message);
            _logger.LogInformation("Reset password email yuborildi: {Email}", toEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Email yuborishda xatolik: {Email}", toEmail);
        }
    }
}