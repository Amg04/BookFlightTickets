namespace BookFlightTickets.Core.Domain.Entities
{
    public class Airport : BaseClass
    {
        public string Name { get; set; } = default!;
        public string Code  { get; set; } = default!; // CAI , DXB
        public string? City  { get; set; }
        public string? Country  { get; set; }
        public ICollection<Flight> DepartureFlights { get; set; } = new HashSet<Flight>();
        public ICollection<Flight> ArrivalFlights { get; set; } = new HashSet<Flight>();
    }
}
