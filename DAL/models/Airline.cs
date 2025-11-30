using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace DAL.models
{
    public class Airline : BaseClass
    {
        public string Name { get; set; }
        public string Code { get; set; } // MS
        [ValidateNever]
        public ICollection<Flight> Flights { get; set; } = new HashSet<Flight>();
        [ValidateNever]
        public ICollection<Airplane> Airplanes { get; set; } = new HashSet<Airplane>();
    }
}
