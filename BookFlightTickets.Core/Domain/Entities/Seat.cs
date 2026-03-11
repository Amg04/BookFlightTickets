namespace BookFlightTickets.Core.Domain.Entities
{
    public class Seat : BaseClass
    {
        public int AirplaneId { get; set; } 
        public Airplane Airplane { get; set; } = default!;
        public string Row { get; set; } = default!;
        public short Number { get; set; } 
        public SeatClass Class { get; set; }
        public int Price { get; set; }
        public ICollection<Ticket> Tickets { get; set; } = new HashSet<Ticket>();
        public ICollection<FlightSeat> FlightSeats { get; set; } = new HashSet<FlightSeat>();
    }

    public enum SeatClass
    {
        Economy = 1,
        Business = 2,
        First = 3
    }
}
