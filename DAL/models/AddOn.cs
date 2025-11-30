using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace DAL.models
{
    public class AddOn : BaseClass
    {
        public string Name { get; set; }
        public short price { get; set; }
        [ValidateNever]
        public ICollection<TicketAddOns> TicketAddOns { get; set; } = new HashSet<TicketAddOns>();
    }
}
