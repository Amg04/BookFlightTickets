namespace BookFlightTickets.Core.Domain.Entities
{
    public class Booking : BaseClass
    {
        public string UserId { get; set; } = default!;
        public AppUser AppUser { get; set; } = default!;
        public int FlightId { get; set; } 
        public Flight Flight { get; set; } = default!;
        public DateTime BookingDate { get; set; }
        public string PNR { get; set; } = default!; // unique
        public decimal TotalPrice { get; set; }
        public Status Status { get; set; }
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
        public Payment? Payment { get; set; }
        public ICollection<Ticket> Tickets { get; set; } = new HashSet<Ticket>();
    }

    public enum Status
    {
        Pending = 1,
        Confirmed = 2,
        Cancelled = 3
    }
}
