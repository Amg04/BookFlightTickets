namespace BookFlightTickets.Core.Domain.Entities
{
    public class FlightSeat : BaseClass
    {
        public int FlightId { get; set; }
        public Flight Flight { get; set; } = default!;
        public int SeatId { get; set; }
        public Seat Seat { get; set; } = default!;
        public int? TicketId { get; set; }
        public Ticket? Ticket { get; set; }
        public bool IsAvailable { get; set; } = true;
    }
}
