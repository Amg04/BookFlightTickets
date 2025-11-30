using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace DAL.models
{
    public class Ticket : BaseClass
    {
        public int BookingID { get; set; } 
        [ValidateNever]
        public Booking Booking { get; set; } = null!;
        public string TicketNumber { get; set; }
        public int FlightSeatId { get; set; }
        public string FirstName { get; set; }
        public string? LastName { get; set; }
        public string PassportNumber { get; set; }
        public decimal TicketPrice { get; set; }
        [ValidateNever]
        public FlightSeat FlightSeat { get; set; } = null!;
        [ValidateNever]
        public ICollection<TicketAddOns> TicketAddOns { get; set; } = new HashSet<TicketAddOns>();

    }
}
