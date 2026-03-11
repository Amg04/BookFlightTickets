namespace BookFlightTickets.Core.Domain.Entities
{
    public class Airplane : BaseClass
    {

        public int AirlineId { get; set; } 
        public Airline Airline { get; set; } = default!;
        public string Model { get; set; } = default!;
        public short SeatCapacity { get; set; }
        public ICollection<Flight> Flights { get; set; } = new HashSet<Flight>();
        public ICollection<Seat> SeatTemplates { get; set; } = new HashSet<Seat>();

    }
}
