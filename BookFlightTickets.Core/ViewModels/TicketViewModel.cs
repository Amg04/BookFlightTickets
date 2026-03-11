using BookFlightTickets.Core.Domain.Entities;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
namespace BookFlightTickets.Core.ViewModels
{
    public class TicketViewModel
    {
        public int BookingID { get; set; }
        [ValidateNever]
        public Booking Booking { get; set; } = default!;
        public string TicketNumber { get; set; } = default!;
        public int FlightSeatId { get; set; }
        public string FirstName { get; set; } = default!;
        public string? LastName { get; set; }
        public string PassportNumber { get; set; } = default!;
        public decimal TicketPrice { get; set; }
        [ValidateNever]
        public FlightSeat FlightSeat { get; set; } = null!;

        #region Mapping

        public static explicit operator TicketViewModel(Ticket model)
        {
            return new TicketViewModel
            {
                BookingID = model.BookingID,
                Booking = model.Booking,
                TicketNumber = model.TicketNumber,
                FlightSeatId = model.FlightSeatId,
                FirstName = model.FirstName,
                LastName = model.LastName,
                PassportNumber = model.PassportNumber,
                TicketPrice = model.TicketPrice,
                FlightSeat = model.FlightSeat
            };
        }

        public static explicit operator Ticket(TicketViewModel ViewModel)
        {
            return new Ticket
            {
                BookingID = ViewModel.BookingID,
                TicketNumber = ViewModel.TicketNumber,
                FlightSeatId = ViewModel.FlightSeatId,
                FirstName = ViewModel.FirstName,
                LastName = ViewModel.LastName,
                PassportNumber = ViewModel.PassportNumber,
                TicketPrice = ViewModel.TicketPrice,
            };
        }

        #endregion
    }
}
