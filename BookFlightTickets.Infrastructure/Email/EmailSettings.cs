namespace BookFlightTickets.Infrastructure.Email
{ 
    public class EmailSettings
    {
        public string SmtpServer { get; set; } = default!;
        public int Port { get; set; }
        public string SenderName { get; set; } = default!; 
        public string SenderEmail { get; set; } = default!;
        public string Username { get; set; } = default!;
        public string Password { get; set; } = default!;
    }

}
