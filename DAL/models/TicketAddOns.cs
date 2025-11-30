using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace DAL.models
{
    public class TicketAddOns : BaseClass
    {
        public int TicketId { get; set; } 
        [ValidateNever]
        public Ticket Ticket { get; set; } = null!;
        public int AddOnID { get; set; } 
        [ValidateNever]
        public AddOn AddOn { get; set; } = null!;
    }
}