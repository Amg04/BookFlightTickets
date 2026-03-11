using BookFlightTickets.Core.Domain.Entities;
using BookFlightTickets.Core.Domain.RepositoryContracts;
using BookFlightTickets.Core.Domain.Specifications;
using BookFlightTickets.Core.Shared.Utility;
using BookFlightTickets.Core.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace BookFlightTickets.UI.Areas.Admin.Controllers
{
    [Area(SD.Admin)]
    [Authorize(Roles = SD.Admin)]
    public class TicketsController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<TicketsController> _logger;

        public TicketsController(
            IUnitOfWork unitOfWork,
            ILogger<TicketsController> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        #region Index

        public async Task<IActionResult> Index()
        {
            try
            {
                _logger.LogInformation("Attempting to get all tickets from database");

                var spec = new BaseSpecification<Ticket>();
                spec.ComplexIncludes.Add(c => c.Include(t => t.FlightSeat)
                    .ThenInclude(a => a.Seat));

                var ticketsFromDb = await _unitOfWork.Repository<Ticket>().GetAllWithSpecAsync(spec);

                if (ticketsFromDb == null)
                {
                    _logger.LogWarning("No tickets found in database");
                    ViewBag.InfoMessage = "No tickets available.";
                    return View(Enumerable.Empty<TicketViewModel>());
                }

                var tickets = ticketsFromDb.Select(t => (TicketViewModel)t).ToList();

                if (!tickets.Any())
                {
                    _logger.LogInformation("No tickets found in database");
                    ViewBag.InfoMessage = "No tickets available.";
                    return View(tickets);
                }
                _logger.LogInformation("Returning {Count} tickets", tickets.Count);
                return View(tickets);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while retrieving tickets in Index action");
                ViewBag.ErrorMessage = "An error occurred while retrieving tickets.";
                return View(Enumerable.Empty<TicketViewModel>());

            }
        }

        #endregion
    }
}
