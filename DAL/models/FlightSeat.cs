using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace DAL.models
{
    public class FlightSeat : BaseClass
    {
        public int FlightId { get; set; }
        public Flight Flight { get; set; } = null!;

        public int SeatId { get; set; }
        public Seat Seat { get; set; } = null!;
        public int? TicketId { get; set; }
        [ValidateNever]
        public Ticket? Ticket { get; set; }
        public bool IsAvailable { get; set; } = true;
    }
}
