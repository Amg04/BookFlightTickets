namespace BookFlightTickets.Core.ViewModels
{
    public class paymentViewModel
    {
        public string SuccessUrl { get; set; } = default!;
        public string CancelUrl { get; set; } = default!;
        public string? SessionId { get; set; }
    }
}
