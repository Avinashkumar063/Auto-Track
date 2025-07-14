using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace AutoTrack.Services
{
    public class EmailService
    {
        private readonly IConfiguration _config;

        public EmailService(IConfiguration config)
        {
            _config = config;
        }

        public async Task SendTaskAssignmentEmailAsync(string toEmail, string toName, string taskTitle)
        {
            var smtpHost = _config["Email:Smtp"];
            var smtpPort = int.Parse(_config["Email:Port"]);
            var smtpUser = _config["Email:Username"];
            var smtpPass = _config["Email:Password"];
            var fromAddress = _config["Email:From"];

            var subject = "New Task Assigned";
            var body = $"Hello {toName},\n\nA new task \"{taskTitle}\" has been assigned to you.\n\nPlease check your dashboard for details.";

            using var client = new SmtpClient(smtpHost, smtpPort)
            {
                Credentials = new NetworkCredential(smtpUser, smtpPass),
                EnableSsl = true
            };
            var mail = new MailMessage(fromAddress, toEmail, subject, body);
            await client.SendMailAsync(mail);
        }
    }
}