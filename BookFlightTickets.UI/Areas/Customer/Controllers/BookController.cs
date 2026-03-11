using BookFlightTickets.Core.Domain.Entities;
using BookFlightTickets.Core.Domain.RepositoryContracts;
using BookFlightTickets.Core.Domain.Specifications;
using BookFlightTickets.Core.ServiceContracts;
using BookFlightTickets.Core.Shared.Utility;
using BookFlightTickets.Core.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Security.Claims;
namespace BookFlightTickets.UI.Areas.Customer.Controllers
{
    [Area(SD.Customer)]
    [Authorize]
    public class BookController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IBookingService _bookingService;
        private readonly ILogger<BookController> _logger;
        private readonly IPdfService _pdfService;
        private readonly IEmailSender _emailSender;

        public BookController(
            IUnitOfWork unitOfWork,
            IBookingService bookingService,
            ILogger<BookController> logger,
            IPdfService pdfService,
            IEmailSender emailSender)
        {
            _unitOfWork = unitOfWork;
            _bookingService = bookingService;
            _logger = logger;
            _pdfService = pdfService;
            _emailSender = emailSender;
        }

        #region Book

        [HttpGet]
        public async Task<IActionResult> Book(int flightId, int ticketCount)
        {
            _logger.LogInformation("Starting booking process - Flight ID: {FlightId}, Ticket count: {TicketCount}", flightId, ticketCount);

            if (ticketCount <= 0)
            {
                _logger.LogWarning("Invalid ticket count requested: {TicketCount}", ticketCount);
                TempData["error"] = "Invalid ticket count. Please select at least one ticket.";
                return RedirectToAction("Index", "Flight", new { area = SD.Customer });
            }

            var result = await _bookingService.GetBookingAsync(flightId, ticketCount);

            if (!result.IsSuccess || result.Value is null)
            {
                _logger.LogWarning("Failed to get booking view model - Flight ID: {FlightId}, Error: {ErrorMessage}", flightId, result.Error?.Message);
                ViewBag.ErrorMessage = result.Error?.Message ?? "Seats not available for the requested flight.";
                ViewBag.FlightId = flightId;
                return View("SeatsNotAvailable");
            }
            _logger.LogInformation("Booking view model retrieved successfully - Flight ID: {FlightId}", flightId);
            return View(result.Value);
        }

        [HttpPost]
        public async Task<IActionResult> Book(BookingCreateViewModel model)
        {
            _logger.LogInformation("Submitting booking - Flight ID: {FlightId}, {TicketCount} tickets", model.FlightId, model.Tickets?.Count ?? 0);

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("Unauthorized booking attempt - User ID not found");
                ViewBag.ErrorMessage = "You must be logged in to make a booking.";
                return Unauthorized();
            }

            try
            {
                var bookingResult = await _bookingService.CreateBookingAsync(model, userId);
                if (!bookingResult.IsSuccess)
                {
                    _logger.LogError("Failed to create booking - User ID: {UserId}, Error: {ErrorMessage}", userId, bookingResult.Error?.Message);
                    await PopulateSeatsAndAddOnsAsync(model);
                    ViewBag.ErrorMessage = bookingResult.Error?.Message ?? "Invalid tickets data. Please try again.";
                    return View(model);
                }

                var bookingId = bookingResult.Value;
                _logger.LogInformation("Booking created successfully - Booking ID: {BookingId}", bookingId);
                var totalAmount = model.Tickets!.Sum(t => t.TicketPrice);
                var paymentCreateResult = await _bookingService.CreatePaymentAsync(bookingId, totalAmount);
                if (!paymentCreateResult.IsSuccess || paymentCreateResult.Value is null)
                {
                    _logger.LogError("Failed to create payment - Booking ID: {BookingId}, Error: {ErrorMessage}", bookingId, paymentCreateResult.Error?.Message);
                    await PopulateSeatsAndAddOnsAsync(model);
                    ViewBag.ErrorMessage = paymentCreateResult.Error?.Message ?? "Failed to create payment. Please try again.";
                    return View(model);
                }

                var payment = paymentCreateResult.Value;
                _logger.LogInformation("Payment created successfully - Payment ID: {PaymentId}, Amount: {Amount}", payment.Id, totalAmount);
                var successUrl = Url.Action("Success", "Book", new { area = SD.Customer, id = payment.Id }, Request.Scheme);
                var cancelUrl = Url.Action("Cancel", "Book", new { area = SD.Customer, id = payment.Id }, Request.Scheme);
                var stripeResult = await _bookingService.CreateStripeSessionAsync(payment, model, successUrl!, cancelUrl!);
                if (!stripeResult.IsSuccess || stripeResult.Value is null)
                {
                    _logger.LogError("Failed to create Stripe session - Payment ID: {PaymentId}, Error: {ErrorMessage}", payment.Id, stripeResult.Error?.Message);
                    ViewBag.ErrorMessage = stripeResult.Error?.Message ?? "Failed to create payment session. Please try again.";
                    return View(model);
                }

                _logger.LogInformation("Stripe session created successfully - Payment ID: {PaymentId}", payment.Id);
                return Redirect(stripeResult.Value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during booking submission - User ID: {UserId}, Flight ID: {FlightId}",
                  userId, model.FlightId);

                await PopulateSeatsAndAddOnsAsync(model);
                ViewBag.ErrorMessage = "An unexpected error occurred during booking. Please try again.";
                return View(model);
            }

        }

        #endregion

        #region Success

        public async Task<IActionResult> Success(int id)
        {
            _logger.LogInformation("Processing payment success - Payment ID: {PaymentId}", id);
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("Unauthorized success access - User ID not found");
                ViewBag.ErrorMessage = "You must be logged in to view this page.";
                return Unauthorized();
            }

            try
            {
                var bookingResult = await _bookingService.ConfirmPaymentAsync(id, userId);
                if (!bookingResult.IsSuccess || bookingResult.Value is null || bookingResult.Value.AppUser?.Email == null)
                {
                    _logger.LogError("Failed to confirm payment - Payment ID: {PaymentId}, User ID: {UserId}, Error: {ErrorMessage}",
                       id, userId, bookingResult.Error?.Message);

                    TempData["error"] = bookingResult.Error?.Message ?? "Payment confirmation failed.";
                    return RedirectToAction("Index", "MyBookings", new { area = SD.Customer });
                }

                var booking = bookingResult.Value;
                _logger.LogInformation("Payment confirmed successfully - Booking ID: {BookingId}, PNR: {PNR}", booking.Id, booking.PNR);
               
                var pdfBytes = await _pdfService.GenerateBookingPdfAsync(booking, ViewData, new ActionContext(HttpContext, RouteData, ControllerContext.ActionDescriptor));

                _logger.LogDebug("PDF generated successfully - Booking ID: {BookingId}", booking.Id);

                await _emailSender.SendEmailAsyncWithAttachment(
                    booking.AppUser.Email,
                    $"Your Booking Confirmation - {booking.PNR}",
                    "Dear Customer,<br/><br/>Your booking has been confirmed successfully!<br/><br/>Please find your e-ticket attached.",
                    pdfBytes,
                    $"Booking_{booking.PNR}.pdf"
                );
                _logger.LogInformation("Confirmation email sent successfully - Email: {Email}, Booking ID: {BookingId}",
                   booking.AppUser.Email, booking.Id);
                TempData["success"] = $"Booking confirmed successfully! Your PNR is: {booking.PNR}. A confirmation email has been sent.";
                return View(booking.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in payment success - Payment ID: {PaymentId}, User ID: {UserId}",
                    id, userId);

                TempData["error"] = "An error occurred while confirming your payment. Please contact support.";
                return RedirectToAction("Index", "MyBookings", new { area = SD.Customer });
            }
        }

        #endregion

        #region Cancel

        public async Task<IActionResult> Cancel(int id)
        {
            _logger.LogInformation("Processing booking cancellation - Payment ID: {PaymentId}", id);

            try
            {
                var result = await _bookingService.CancelBookingAsync(id);
                if (!result.IsSuccess)
                {
                    _logger.LogError("Failed to cancel booking - Payment ID: {PaymentId}, Error: {ErrorMessage}", id, result.Error?.Message);
                    ViewBag.ErrorMessage = result.Error?.Message ?? "Failed to cancel booking. Please try again.";
                }
                else
                {
                    _logger.LogInformation("Booking cancelled successfully - Payment ID: {PaymentId}", id);
                    TempData["success"] = "Booking cancelled successfully. No charges have been made to your account.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during booking cancellation - Payment ID: {PaymentId}", id);
                ViewBag.ErrorMessage = "An unexpected error occurred while cancelling the booking.";
            }

            return View();
        }

        #endregion

        #region Helper

        private async Task PopulateSeatsAndAddOnsAsync(BookingCreateViewModel model)
        {
            _logger.LogDebug("Populating seats and add-ons for Flight ID: {FlightId}", model.FlightId);

            try
            {
                var seatSpec = new BaseSpecification<FlightSeat>(s => s.FlightId == model.FlightId && s.IsAvailable);
                seatSpec.Includes.Add(fs => fs.Seat);

                var flightSeats = await _unitOfWork.Repository<FlightSeat>().GetAllWithSpecAsync(seatSpec);
                model.AvailableSeats = flightSeats
                    .Where(s => s.Seat != null)
                    .Select(s => new SelectListItem
                    {
                        Value = s.Id.ToString(),
                        Text = $"{s.Seat.Row}{s.Seat.Number} ({s.Seat.Class}) {s.Seat.Price}$"
                    }).ToList();

                model.AddOns = (await _unitOfWork.Repository<AddOn>().GetAllAsync()).ToList();
                _logger.LogDebug("Seats and add-ons populated - {SeatCount} seats, {AddOnCount} add-ons",model.AvailableSeats.Count, model.AddOns.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error populating seats and add-ons for Flight ID: {FlightId}", model.FlightId);
                throw;
            }
           
        }


        #endregion

    }
}
