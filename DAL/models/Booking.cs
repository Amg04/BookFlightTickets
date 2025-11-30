using BAL.model;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace DAL.models
{
    public class Booking : BaseClass
    {
        public string UserId { get; set; }
        [ValidateNever]
        public AppUser AppUser { get; set; } = null!;
        public int FlightId { get; set; } 
        [ValidateNever]
        public Flight Flight { get; set; } = null!;
        public DateTime BookingDate { get; set; }
        public string PNR { get; set; } // unique
        public decimal TotalPrice { get; set; }
        public Status Status { get; set; }
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
        [ValidateNever]
        public Payment? Payment { get; set; }

        [ValidateNever]
        public ICollection<Ticket> Tickets { get; set; } = new HashSet<Ticket>();
    }

    public enum Status
    {
        Pending = 1,
        Confirmed = 2,
        Cancelled = 3
    }
}
