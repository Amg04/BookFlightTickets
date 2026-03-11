namespace BookFlightTickets.Core.Domain.Entities
{
    public class Airline : BaseClass
    {
        public string Name { get; set; } = default!;
        public string Code { get; set; } = default!; // MS
        public ICollection<Flight> Flights { get; set; } = new HashSet<Flight>();
        public ICollection<Airplane> Airplanes { get; set; } = new HashSet<Airplane>();
    }
}
