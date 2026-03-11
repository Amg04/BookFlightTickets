namespace BookFlightTickets.Core.Domain.Entities
{
    public class Ticket : BaseClass
    {
        public int BookingID { get; set; } 
        public Booking Booking { get; set; } = default!;
        public string TicketNumber { get; set; } = default!;
        public int FlightSeatId { get; set; } 
        public string FirstName { get; set; } = default!;
        public string? LastName { get; set; }
        public string PassportNumber { get; set; } = default!;
        public decimal TicketPrice { get; set; }
        public FlightSeat FlightSeat { get; set; } = default!;
        public ICollection<TicketAddOns> TicketAddOns { get; set; } = new HashSet<TicketAddOns>();

    }
}
