using BookFlightTickets.Core.Domain.Entities;
using BookFlightTickets.Core.Domain.RepositoryContracts;
using BookFlightTickets.Core.Domain.Specifications;
using BookFlightTickets.Core.Shared.Utility;
using BookFlightTickets.Core.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace BookFlightTickets.UI.Areas.Admin.Controllers
{
    [Area(SD.Admin)]
    [Authorize(Roles = SD.Admin)]
    public class BookingsController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<BookingsController> _logger;

        public BookingsController(
            IUnitOfWork unitOfWork,
            ILogger<BookingsController> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        #region Index

        public async Task<IActionResult> Index()
        {
            try
            {
                _logger.LogInformation("Attempting to get all bookings from database");

                var spec = new BaseSpecification<Booking>();
                spec.Includes.Add(b => b.Payment!);

                var bookingsFromDb = await _unitOfWork.Repository<Booking>().GetAllWithSpecAsync(spec);

                if (bookingsFromDb == null)
                {
                    _logger.LogWarning("Database returned null for bookings");
                    ViewBag.InfoMessage = "No bookings available.";
                    return View(Enumerable.Empty<BookingViewModel>());
                }

                var bookings = bookingsFromDb.Select(a => (BookingViewModel)a).ToList();


                if (!bookings.Any())
                {
                    _logger.LogInformation("No bookings found in database");
                    ViewBag.InfoMessage = "No bookings available.";
                    return View(bookings);
                }
                _logger.LogInformation("Returning {Count} bookings", bookings.Count);
                return View(bookings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while retrieving bookings in Index action");
                ViewBag.ErrorMessage = "An error occurred while retrieving bookings.";
                return View(Enumerable.Empty<BookingViewModel>());
            }
        }

        #endregion

    }
}
