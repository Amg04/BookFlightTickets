namespace BookFlightTickets.Core.Domain.RepositoryContracts
{
    public interface IEmailSender
    {
        Task SendEmailAsync(string email, string subject, string htmlMessage);
        Task SendEmailAsyncWithAttachment(string toEmail, string subject, string body, byte[] attachment, string attachmentName);
    }
}
