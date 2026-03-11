namespace BookFlightTickets.Core.Domain.Entities
{
    public class Flight : BaseClass
    {
        public int AirlineId { get; set; } 
        public Airline Airline { get; set; } = default!;
        public int AirplaneId { get; set; } 
        public Airplane Airplane { get; set; } = default!;
        public int DepartureAirportID { get; set; }
        public Airport DepartureAirport { get; set; } = default!;
        public int ArrivalAirportID { get; set; } 
        public Airport ArrivalAirport { get; set; } = default!;
        public DateTime DepartureTime { get; set; }
        public DateTime ArrivalTime { get; set; }
        public decimal BasePrice { get; set; }
        public FlightStatus Status { get; set; } = FlightStatus.Scheduled;
        public ICollection<Booking> Bookings { get; set; } = new HashSet<Booking>();
        public ICollection<FlightSeat> FlightSeats { get; set; } = new HashSet<FlightSeat>();
    }

    public enum FlightStatus
    {
        Scheduled = 1,      // مجدول - الرحلة في الجدول وتنتظر
        Boarding = 2,       // الصعود - الركاب يصعدون الطائرة
        Departed = 3,       // غادر - الطائرة أقلعت
        InAir = 4,          // في الجو - الطائرة في مسارها
        Landed = 5,         // هبط - وصلت المطار المقصود
        Arrived = 6,        // وصلت - الركاب يغادرون الطائرة
        Delayed = 7,        // مؤجل - تأخير قبل الإقلاع
        Cancelled = 8,      // ملغي - تم إلغاء الرحلة
        Diverted = 9        // تحويل - تم تحويلها لمطار آخر
    }

}
