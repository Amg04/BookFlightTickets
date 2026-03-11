namespace BookFlightTickets.Core.Domain.Entities
{
    public class AddOn : BaseClass
    {
        public string Name { get; set; } = default!;
        public short Price { get; set; }
        public ICollection<TicketAddOns> TicketAddOns { get; set; } = new HashSet<TicketAddOns>();
    }
}
