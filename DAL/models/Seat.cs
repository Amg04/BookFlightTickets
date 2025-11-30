using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace DAL.models
{
    public class Seat : BaseClass
    {
        public int AirplaneId { get; set; } 
        [ValidateNever]
        public Airplane Airplane { get; set; } = null!;
        public string Row { get; set; }  
        public short Number { get; set; } 

        public SeatClass Class { get; set; }
        public int Price { get; set; }
        [ValidateNever]
        public ICollection<Ticket> Tickets { get; set; } = new HashSet<Ticket>();
        [ValidateNever]
        public ICollection<FlightSeat> FlightSeats { get; set; } = new HashSet<FlightSeat>();
    }

    public enum SeatClass
    {
        Economy = 1,
        Business = 2,
        First = 3
    }
}
