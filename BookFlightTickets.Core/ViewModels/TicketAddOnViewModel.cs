using BookFlightTickets.Core.Domain.Entities;
namespace BookFlightTickets.Core.ViewModels
{
    public class TicketAddOnViewModel
    {
        public int TicketId { get; set; }
        public int AddOnID { get; set; }
       

        #region Mapping

        public static explicit operator TicketAddOnViewModel(TicketAddOns model)
        {
            return new TicketAddOnViewModel
            {
                TicketId = model.TicketId,
                AddOnID = model.AddOnID,
            };
        }

        public static explicit operator TicketAddOns(TicketAddOnViewModel ViewModel)
        {
            return new TicketAddOns
            {
                TicketId = ViewModel.TicketId,
                AddOnID = ViewModel.AddOnID,
            };
        }

        #endregion
    }
}
