using BookFlightTickets.Core.Domain.RepositoryContracts;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;

namespace BookFlightTickets.Infrastructure.Email
{
    public class SmtpEmailSender : IEmailSender
    {
        private readonly EmailSettings _emailSettings;

        public SmtpEmailSender(IOptions<EmailSettings> emailSettings)
        {
            _emailSettings = emailSettings.Value;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            var message = new MailMessage
            {
                From = new MailAddress(_emailSettings.SenderEmail, _emailSettings.SenderName),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };
            message.To.Add(toEmail);

            using var client = new SmtpClient(_emailSettings.SmtpServer, _emailSettings.Port)
            {
                Credentials = new NetworkCredential(_emailSettings.Username, _emailSettings.Password),
                EnableSsl = true
            };

            await client.SendMailAsync(message);
        }

        public async Task SendEmailAsyncWithAttachment(string toEmail, string subject, string body, byte[] attachment, string attachmentName)
        {
            try
            {
                var message = new MailMessage
                {
                    From = new MailAddress(_emailSettings.SenderEmail, _emailSettings.SenderName),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true
                };
                message.To.Add(toEmail);

                var attachmentData = new Attachment(new MemoryStream(attachment), attachmentName);
                message.Attachments.Add(attachmentData);

                using var client = new SmtpClient(_emailSettings.SmtpServer, _emailSettings.Port)
                {
                    Credentials = new NetworkCredential(_emailSettings.Username, _emailSettings.Password),
                    EnableSsl = true,
                    Timeout = 30000
                };

                await client.SendMailAsync(message);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed To send email: {ex.Message}", ex);
            }
        }
    }
}
