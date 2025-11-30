using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace DAL.models
{
    public class Airplane : BaseClass
    {

        public int AirlineId { get; set; } 
        [ValidateNever]
        public Airline Airline { get; set; } = null!;
        public string Model { get; set; } 
        public short SeatCapacity { get; set; }
        [ValidateNever]
        public ICollection<Flight> Flights { get; set; } = new HashSet<Flight>();
        [ValidateNever]
        public ICollection<Seat> SeatTemplates { get; set; } = new HashSet<Seat>();

    }
}
