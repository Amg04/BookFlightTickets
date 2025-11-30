using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace DAL.models
{
    public class Airport : BaseClass
    {
        public string Name  { get; set; }
        public string Code  { get; set; } // CAI , DXB
        public string? City  { get; set; }
        public string? Country  { get; set; }
        [ValidateNever]
        public ICollection<Flight> DepartureFlights { get; set; } = new HashSet<Flight>();
        [ValidateNever]
        public ICollection<Flight> ArrivalFlights { get; set; } = new HashSet<Flight>();
    }
}
