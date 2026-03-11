namespace BookFlightTickets.Core.Domain.Entities
{
    public class TicketAddOns : BaseClass
    {
        public int TicketId { get; set; } 
        public Ticket Ticket { get; set; } = default!;
        public int AddOnID { get; set; } 
        public AddOn AddOn { get; set; } = default!;
    }
}