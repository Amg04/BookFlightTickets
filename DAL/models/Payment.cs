using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace DAL.models
{
    public class Payment : BaseClass
    {
        public int BookingID { get; set; }
        [ValidateNever]
        public Booking Booking { get; set; } = null!;
        public string? SessionId { get; set; }
        public string? PaymentIntentId { get; set; }
        public decimal Amount { get; set; }  
        public DateTime PaymentDate { get; set; } 
        public PaymentStatus PaymentStatus { get; set; }
    }

    public enum PaymentStatus
    {
        Pending = 1,
        Approved = 2,
        Refunded = 3,
    }
}
