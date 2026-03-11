using BookFlightTickets.Core.Domain.Entities;
using BookFlightTickets.Core.Domain.RepositoryContracts;
using BookFlightTickets.Core.Domain.Specifications;
using BookFlightTickets.Core.ServiceContracts;
using BookFlightTickets.Core.Shared.Utility;
using BookFlightTickets.Core.ViewModels;
using Microsoft.AspNetCore.Mvc;
using X.PagedList;
namespace BookFlightTickets.UI.Areas.Customer.Controllers
{
    [Area(SD.Customer)]
    public class FlightController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IFlightService _flightService;
        private readonly ILogger<FlightController> _logger;

        public FlightController(
            IUnitOfWork unitOfWork,
            IFlightService flightService,
            ILogger<FlightController> logger)
        {
            _unitOfWork = unitOfWork;
            _flightService = flightService;
            _logger = logger;
        }

        #region Index

        [HttpGet]
        public async Task<IActionResult> Index(FlightFilterViewModel obj)
        {
            _logger.LogInformation("FlightController.Index called with parameters: {@FilterModel}", obj);
            try
            {
                SetupViewBag(obj);
                _logger.LogDebug("ViewBag setup completed for flight search");

                var (flights, errors) = await ProcessFlightData(obj);
                if (!errors.Any() && flights == null)
                {
                    _logger.LogWarning("Flights list is null after processing");
                    flights = new List<FlightViewModel>();
                    errors.Add("Failed to retrieve flight data.");
                }
                DisplayResultMessages(errors);
                var pagedFlights = SetupPagination(flights, obj.Page, obj.PageSize);

                return View(pagedFlights);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in flight search with filters: {@FilterModel}", obj);
                ViewBag.ErrorMessage = "An unexpected error occurred while searching for flights.";
                return View(new List<FlightViewModel>().ToPagedList(1, 10));
            }
        }

        #endregion

        #region Details

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            _logger.LogInformation("Flight details requested for ID: {FlightId}", id);

            try
            {
                var spec = new BaseSpecification<Flight>(f => f.Id == id);
                spec.Includes.Add(f => f.Airline);
                spec.Includes.Add(f => f.Airplane);
                spec.Includes.Add(f => f.FlightSeats);
                spec.Includes.Add(f => f.DepartureAirport);
                spec.Includes.Add(f => f.ArrivalAirport);

                _logger.LogDebug("Fetching flight details from database for ID: {FlightId}", id);
                var flight = await _unitOfWork.Repository<Flight>().GetEntityWithSpecAsync(spec);
                if (flight == null)
                {
                    _logger.LogWarning("Flight not found with ID: {FlightId}", id);
                    return NotFound();
                }

                _logger.LogInformation("Flight details retrieved successfully for ID: {FlightId}", id);
                return View((FlightViewModel)flight);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving flight details for ID: {FlightId}", id);
                TempData["error"] = "An error occurred while loading flight details.";
                return RedirectToAction(nameof(Index));
            }
        }

        #endregion

        #region Helper methods

        private void SetupViewBag(FlightFilterViewModel obj)
        {
            ViewBag.SearchFields = new Dictionary<string, string>()
            {
                { nameof(FlightViewModel.Airline.Name), "Airline" },
                { "FromAirport" , "FromAirport" },
                { "ToAirport" , "ToAirport" },
                { nameof(FlightViewModel.BasePrice), "Price" },
                { nameof(FlightViewModel.AvailableSeatsCount), "Available Seats" }
            };

            ViewBag.SortableHeaders = new List<SortableHeader>
            {
                new SortableHeader { SortField = nameof(FlightViewModel.Airline.Name), DisplayName = "Airline" },
                new SortableHeader { SortField = "FromAirport", DisplayName = "FromAirport" },
                new SortableHeader { SortField = "ToAirport", DisplayName = "ToAirport" },
                new SortableHeader { SortField = nameof(FlightViewModel.DepartureTime), DisplayName = "Departure" },
                new SortableHeader { SortField = nameof(FlightViewModel.ArrivalTime), DisplayName = "Arrival" },
                new SortableHeader { SortField = nameof(FlightViewModel.BasePrice), DisplayName = "Price" },
                new SortableHeader { SortField = nameof(FlightViewModel.AvailableSeatsCount), DisplayName = "Available Seats" }
            };

            ViewBag.CurrentSearchBy = obj.SearchBy;
            ViewBag.CurrentSearchString = obj.SearchString;
            ViewBag.CurrentSortBy = obj.SortBy;
            ViewBag.CurrentSortOrder = obj.SortOrder.ToString();
            ViewBag.FromDate = obj.FromDate?.ToString("yyyy-MM-dd");
            ViewBag.ToDate = obj.ToDate?.ToString("yyyy-MM-dd");
            ViewBag.FilterModel = obj;
        }

        private async Task<(List<FlightViewModel> Flights, List<string> Errors)> ProcessFlightData(FlightFilterViewModel obj)
        {
            _logger.LogDebug("Processing flight data with search criteria");

            var errors = new List<string>();
            List<FlightViewModel> flights;

            if (obj.FromDate.HasValue && obj.ToDate.HasValue && obj.FromDate > obj.ToDate)
            {
                string errorMessage = "From date cannot be after To date";
                _logger.LogWarning("Date validation error: {ErrorMessage}", errorMessage);
                errors.Add(errorMessage);
                return (new List<FlightViewModel>(), errors);
            }

            var filteredResult = await _flightService.GetFilteredFlights(
                obj.SearchBy,
                obj.SearchString,
                obj.FromDate,
                obj.ToDate);

            if (!filteredResult.IsSuccess)
            {
                string errorMessage = filteredResult.Error?.Message ?? "Filtering failed";
                _logger.LogError("Flight service filtering error: {ErrorMessage}", errorMessage);
                errors.Add(errorMessage);
                flights = new List<FlightViewModel>();
            }
            else
            {
                flights = filteredResult.Value ?? new List<FlightViewModel>();
                _logger.LogDebug("Flight filtering returned {FlightCount} results", flights.Count);
            }

            if (flights.Any())
            {
                var sortedResult = await _flightService.GetSortedFlightsAsync(
                    flights,
                    obj.SortBy,
                    obj.SortOrder);

                if (!sortedResult.IsSuccess)
                {
                    var errorMessage = sortedResult.Error?.Message ?? "Sorting failed";
                    _logger.LogError("Flight service sorting error: {ErrorMessage}", errorMessage);
                    errors.Add(errorMessage);
                }
                else
                {
                    flights = sortedResult.Value ?? flights;
                    _logger.LogDebug("Flights sorted successfully");
                }
            }

            return (flights, errors);
        }

        private void DisplayResultMessages(List<string> errors)
        {
            if (errors.Any())
            {
                var errorMessage = string.Join("; ", errors);
                TempData["error"] = errorMessage;
                _logger.LogDebug("Displaying error message to user: {ErrorMessage}", errorMessage);
            }
        }

        private IPagedList<FlightViewModel> SetupPagination(List<FlightViewModel> flights, int page, int pageSize)
        {
            var pageNumber = page <= 0 ? 1 : page;
            var actualPageSize = pageSize <= 0 ? 10 : pageSize;

            var pagedFlights = flights.ToPagedList(pageNumber, actualPageSize);

            ViewBag.CurrentPage = pageNumber;
            ViewBag.TotalPages = pagedFlights.PageCount;
            ViewBag.PageSize = actualPageSize;
            ViewBag.TotalItems = flights.Count;

            _logger.LogDebug("Pagination setup: Page {PageNumber} of {TotalPages}, {PageSize} items per page", pageNumber, pagedFlights.PageCount, actualPageSize);

            return pagedFlights;
        }

        #endregion
    }
}
