namespace BookFlightTickets.Core.Domain.ResultPattern
{
    public class Error
    {
        public string Code { get; }
        public string Message { get; }
        public string? Details { get; }
        public DateTime Timestamp { get; }

        public Error(string code, string message, string? details = null)
        {
            Code = code;
            Message = message;
            Details = details;
            Timestamp = DateTime.UtcNow;
        }
    }
}



   
