using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Options;
using MimeKit;
using MailKit.Net.Smtp;

namespace WhiskeyTracker.Web.Services;

public class EmailSender : IEmailSender
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailSender> _logger;

    public EmailSender(IConfiguration configuration, ILogger<EmailSender> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        var emailSettings = _configuration.GetSection("EmailSettings");
        var host = emailSettings["Host"];
        var port = int.TryParse(emailSettings["Port"], out var parsedPort) ? parsedPort : 587;
        var user = emailSettings["User"];
        var pass = emailSettings["Password"];
        var senderEmail = emailSettings["SenderEmail"] ?? "noreply@whiskeytracker.com";
        var senderName = emailSettings["SenderName"] ?? "Whiskey Tracker";

        if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass))
        {
            _logger.LogWarning("Email sending skipped: EmailSettings not configured.");
            return;
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(senderName, senderEmail));
        message.To.Add(new MailboxAddress("", email));
        message.Subject = subject;

        var bodyBuilder = new BodyBuilder { HtmlBody = htmlMessage };
        message.Body = bodyBuilder.ToMessageBody();

        using var client = new SmtpClient();
        try
        {
            await client.ConnectAsync(host, port, MailKit.Security.SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(user, pass);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
            _logger.LogInformation($"Email sent to {email}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to send email to {email}");
            throw;
        }
    }
}
