using MailKit.Net.Smtp;
using MimeKit;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;

namespace AutoTrack.Services
{
    public class EmailSender
    {
        private readonly IConfiguration _config;
        public EmailSender(IConfiguration config) => _config = config;

        public async Task SendEmailAsync(string toEmail, string subject, string message)
        {
            var email = new MimeMessage();
            email.From.Add(MailboxAddress.Parse(_config["Email:From"]));
            email.To.Add(MailboxAddress.Parse(toEmail));
            email.Subject = subject;
            email.Body = new TextPart(MimeKit.Text.TextFormat.Text) { Text = message };

            using var smtp = new SmtpClient();
            await smtp.ConnectAsync(_config["Email:Smtp"], int.Parse(_config["Email:Port"]), true);
            await smtp.AuthenticateAsync(_config["Email:Username"], _config["Email:Password"]);
            await smtp.SendAsync(email);
            await smtp.DisconnectAsync(true);
        }
    }
}
