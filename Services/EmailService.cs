using System.Net;
using System.Net.Mail;

namespace ECommerce.API.Services;

public class EmailService
{
    private readonly IConfiguration _configuration;

    public EmailService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task SendAsync(string to, string subject, string htmlBody)
    {
        var from = _configuration["Email:From"]!;

        using var client = new SmtpClient(
            _configuration["Email:Host"],
            int.Parse(_configuration["Email:Port"]!))
        {
            Credentials = new NetworkCredential(
                _configuration["Email:User"],
                _configuration["Email:Password"]),
            EnableSsl = true,
        };

        using var message = new MailMessage(from, to, subject, htmlBody)
        {
            IsBodyHtml = true,
        };

        await client.SendMailAsync(message);
    }
}