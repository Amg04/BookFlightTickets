using BookFlightTickets.Core.Domain.Entities;
using BookFlightTickets.Core.Domain.RepositoryContracts;
using BookFlightTickets.Core.Domain.Specifications;
using BookFlightTickets.Core.Shared.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rotativa.AspNetCore;
using System.Security.Claims;
namespace BookFlightTickets.UI.Areas.Customer.Controllers
{
    [Area(SD.Customer)]
    [Authorize]
    public class MyBookingsController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<BookController> _logger;

        public MyBookingsController(IUnitOfWork unitOfWork, ILogger<BookController> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        #region Index
        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("Unauthorized access attempt to MyBookings.Index - User ID not found in claims");
                return Unauthorized();
            }

            _logger.LogInformation("Loading bookings for user ID: {UserId}", userId);

            try
            {
                var spec = new BaseSpecification<Booking>(b => b.UserId == userId);
                spec.ComplexIncludes.Add(c => c.Include(t => t.Tickets)
                      .ThenInclude(a => a.TicketAddOns)
                      .ThenInclude(a => a.AddOn));
                spec.ComplexIncludes.Add(c => c.Include(t => t.Flight)
                    .ThenInclude(a => a.FlightSeats)
                    .ThenInclude(a => a.Seat));
                spec.ComplexIncludes.Add(c => c.Include(t => t.Flight)
                    .ThenInclude(a => a.DepartureAirport));
                spec.ComplexIncludes.Add(c => c.Include(t => t.Flight)
                    .ThenInclude(a => a.ArrivalAirport));
                spec.ComplexIncludes.Add(c => c.Include(t => t.Flight)
                    .ThenInclude(a => a.Airline));
                spec.ComplexIncludes.Add(c => c.Include(t => t.Flight)
                    .ThenInclude(a => a.Airplane));
                spec.Includes.Add(b => b.Payment!);
                spec.OrderByDesc(b => b.Id);

                _logger.LogDebug("Executing booking query with complex includes for user: {UserId}", userId);

                var MyBookings = await _unitOfWork.Repository<Booking>().GetAllWithSpecAsync(spec);

                _logger.LogInformation("Successfully retrieved bookings for user: {UserId}", userId);

                return View(MyBookings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while loading bookings for user ID: {UserId}", userId);
                ViewBag.ErrorMessage = "An error occurred while loading your bookings. Please try again.";
                return View(new List<Booking>());
            }
        }

        #endregion

        #region BookingPDF

        //Rotativa.AspNetCore
        public async Task<IActionResult> BookingPDF(int bookingId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("Unauthorized PDF generation attempt - User ID not found in claims");
                return Unauthorized();
            }

            _logger.LogInformation("Generating PDF for booking ID: {BookingId} for user ID: {UserId}", bookingId, userId);

            try
            {
                var spec = new BaseSpecification<Booking>(b => b.Id == bookingId && b.UserId == userId);
                spec.ComplexIncludes.Add(c => c.Include(t => t.Tickets)
                     .ThenInclude(a => a.TicketAddOns)
                     .ThenInclude(a => a.AddOn));
                spec.ComplexIncludes.Add(c => c.Include(t => t.Flight)
                    .ThenInclude(a => a.FlightSeats)
                    .ThenInclude(a => a.Seat));
                spec.ComplexIncludes.Add(c => c.Include(t => t.Flight)
                    .ThenInclude(a => a.DepartureAirport));
                spec.ComplexIncludes.Add(c => c.Include(t => t.Flight)
                    .ThenInclude(a => a.ArrivalAirport));
                spec.ComplexIncludes.Add(c => c.Include(t => t.Flight)
                    .ThenInclude(a => a.Airline));
                spec.ComplexIncludes.Add(c => c.Include(t => t.Flight)
                    .ThenInclude(a => a.Airplane));
                spec.Includes.Add(b => b.Payment!);
               
                _logger.LogDebug("Querying booking details for PDF generation - Booking ID: {BookingId}", bookingId);

                var booking = await _unitOfWork.Repository<Booking>().GetEntityWithSpecAsync(spec);
                if (booking == null)
                {
                    _logger.LogWarning("Booking not found or access denied - Booking ID: {BookingId}, User ID: {UserId}",
                       bookingId, userId);
                    return NotFound();
                }
                _logger.LogInformation("Successfully retrieved booking for PDF generation - PNR: {PNR}", booking.PNR);
                return new ViewAsPdf("BookingPDF", booking, ViewData)
                {
                    PageMargins = new Rotativa.AspNetCore.Options.Margins() { Top = 20, Right = 20, Bottom = 20, Left = 20 },
                    PageOrientation = Rotativa.AspNetCore.Options.Orientation.Portrait,
                    FileName = $"Booking_{booking.PNR}.pdf",
                    PageSize = Rotativa.AspNetCore.Options.Size.A4
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating PDF for booking ID: {BookingId}, user ID: {UserId}",
                    bookingId, userId);
                TempData["error"] = "An error occurred while generating the booking PDF.";
                return RedirectToAction(nameof(Index));
            }
        }



        #endregion

    }
}
