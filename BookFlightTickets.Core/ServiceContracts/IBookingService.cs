using BookFlightTickets.Core.Domain.Entities;
using BookFlightTickets.Core.Domain.ResultPattern;
using BookFlightTickets.Core.ViewModels;

namespace BookFlightTickets.Core.ServiceContracts
{
    public interface IBookingService
    {
        Task<Result<BookingCreateViewModel>> GetBookingAsync(int flightId, int ticketCount);
        Task<Result<int>> CreateBookingAsync(BookingCreateViewModel model, string userId);
        Task<Result<Payment>> CreatePaymentAsync(int bookingId, decimal totalAmount);
        Task<Result<string>> CreateStripeSessionAsync(
            Payment payment, 
            BookingCreateViewModel model, 
            string successUrl, 
            string cancelUrl);
        Task<Result<Booking>> ConfirmPaymentAsync(int paymentId , string userId);
        Task<Result<bool>> CancelBookingAsync(int paymentId);
        Task<Result<Booking>> GetBookingWithDetailsAsync(int bookingId, string userId);
    }
}
