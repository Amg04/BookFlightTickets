using BookFlightTickets.Core.Domain.Entities;
using BookFlightTickets.Core.Domain.RepositoryContracts;
using BookFlightTickets.Core.Shared.Utility;
using BookFlightTickets.Core.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace BookFlightTickets.UI.Areas.Admin.Controllers
{
    [Area(SD.Admin)]
    [Authorize(Roles = SD.Admin)]
    public class TicketAddOnsController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<TicketAddOnsController> _logger;


        public TicketAddOnsController(
            IUnitOfWork unitOfWork,
            ILogger<TicketAddOnsController> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        #region Index

        public async Task<IActionResult> Index()
        {
            try
            {
                _logger.LogInformation("Attempting to get all ticket add-ons from database");

                var ticketAddOnsFromDb = await _unitOfWork.Repository<TicketAddOns>().GetAllAsync();

                if (ticketAddOnsFromDb == null)
                {
                    _logger.LogWarning("Database returned null for ticket add-ons");
                    ViewBag.InfoMessage = "No ticket add-ons available.";
                    return View(Enumerable.Empty<TicketAddOnViewModel>());
                }

                var ticketAddOns = ticketAddOnsFromDb.Select(a => (TicketAddOnViewModel)a);

                if (!ticketAddOns.Any())
                {
                    _logger.LogInformation("No ticket AddOns found in database");
                    ViewBag.InfoMessage = "No ticket AddOns available.";
                    return View(ticketAddOns);
                }

                _logger.LogInformation("Returning {Count} ticket add-ons", ticketAddOns.Count());
                return View(ticketAddOns);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while retrieving ticket add-ons in Index action");
                ViewBag.ErrorMessage = "An error occurred while retrieving ticket add-ons.";
                return View(Enumerable.Empty<TicketAddOnViewModel>());
            }
        }

        #endregion

    }
}
