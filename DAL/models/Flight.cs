using BAL.model;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace DAL.models
{
    public class Flight : BaseClass
    {
        public int AirlineId { get; set; } 
        [ValidateNever]
        public Airline Airline { get; set; } = null!;
        public int AirplaneId { get; set; } 
        [ValidateNever]
        public Airplane Airplane { get; set; } = null!;
        public int DepartureAirportID { get; set; }
        [ValidateNever]
        public Airport DepartureAirport { get; set; } = null!;
        public int ArrivalAirportID { get; set; } 
        [ValidateNever]
        public Airport ArrivalAirport { get; set; } = null!;
        public DateTime DepartureTime { get; set; }
        public DateTime ArrivalTime { get; set; }
        public decimal BasePrice { get; set; }
        public FlightStatus Status { get; set; } = FlightStatus.Scheduled;
        [ValidateNever]
        public ICollection<Booking> Bookings { get; set; } = new HashSet<Booking>();
        [ValidateNever]
        public ICollection<FlightSeat> FlightSeats { get; set; } = new HashSet<FlightSeat>();
    }

    public enum FlightStatus
    {
        Scheduled = 1,   // مجدول - متاح للحجز
        Cancelled = 2,   // ملغي
        Delayed = 3,     // مؤجل
        Departed = 4,    // غادر
        Landed = 5       // هبط
    }

}
