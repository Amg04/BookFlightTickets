using BookFlightTickets.Core.Domain.Entities;
using BookFlightTickets.Core.Domain.Hubs;
using BookFlightTickets.Core.Domain.RepositoryContracts;
using BookFlightTickets.Core.Domain.Specifications;
using BookFlightTickets.Core.ServiceContracts;
using BookFlightTickets.Core.Shared.Utility;
using BookFlightTickets.Core.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.SignalR;
namespace BookFlightTickets.UI.Areas.Admin.Controllers
{
    [Area(SD.Admin)]
    [Authorize(Roles = SD.Admin)]
    public class FlightController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IWebHostEnvironment _env;
        private readonly IRedisCacheService _cacheService;
        private readonly ILogger<FlightController> _logger;
        private readonly IHubContext<DashboardHub> _hubContext;
        private const string CACHE_KEY_ALL_FLIGHTS = "flights:all";
        private const string CACHE_KEY_FLIGHT_PREFIX = "flight:id:";
        private const string CACHE_KEY_FLIGHT_DETAILS_PREFIX = "flight:details:id:";
        private const string CACHE_KEY_AIRPLANES_BY_AIRLINE_PREFIX = "airplanes:flight:";

        public FlightController(
            IUnitOfWork unitOfWork,
            IWebHostEnvironment env,
            IRedisCacheService cacheService,
            ILogger<FlightController> logger,
            IHubContext<DashboardHub> hubContext)
        {
            _unitOfWork = unitOfWork;
            _env = env;
            _cacheService = cacheService;
            _logger = logger;
            _hubContext = hubContext;
        }

        #region Index

        public async Task<IActionResult> Index()
        {
            try
            {
                _logger.LogInformation("Attempting to get all flights from cache or database");

                var flights = await _cacheService.GetOrSetAsync(
                key: CACHE_KEY_ALL_FLIGHTS,
                factory: async () =>
                {
                    _logger.LogInformation("Cache miss for {CacheKey}. Retrieving from database.", CACHE_KEY_ALL_FLIGHTS);

                    var spec = new BaseSpecification<Flight>();
                    spec.Includes.Add(f => f.Airline);
                    spec.Includes.Add(f => f.Airplane);
                    spec.Includes.Add(f => f.DepartureAirport);
                    spec.Includes.Add(f => f.ArrivalAirport);

                    var flightsFromDb = await _unitOfWork.Repository<Flight>().GetAllWithSpecAsync(spec);

                    if (flightsFromDb == null)
                    {
                        _logger.LogWarning("Database returned null for flights");
                        return new List<FlightViewModel>();
                    }
                    return flightsFromDb.Select(f => (FlightViewModel)f).ToList();
                },
                expiry: TimeSpan.FromMinutes(15));

                if (flights == null || !flights.Any())
                {
                    _logger.LogWarning("No flights found in cache or database. Cache key: {CacheKey}", CACHE_KEY_ALL_FLIGHTS);
                    ViewBag.InfoMessage = "No flights available.";
                    return View(Enumerable.Empty<FlightViewModel>());
                }

                _logger.LogInformation("Returning {Count} flights", flights.Count());
                return View(flights);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while retrieving flights in Index action");
                ViewBag.ErrorMessage = "An error occurred while retrieving flights.";
                return View(new List<FlightViewModel>());
            }
        }

        #endregion

        #region Create

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            try
            {
                await PopulateDropDownLists();
                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while loading create flight view");
                TempData["error"] = "An error occurred while loading the form.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(FlightViewModel flightVM)
        {
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Create Flight failed validation for model: {@FlightViewModel}",
                    new { flightVM.Id, flightVM.AirlineId, flightVM.DepartureAirportID });
                await PopulateDropDownLists(airlineId: flightVM.AirlineId);
                return View(flightVM);
            }

            try
            {
                _logger.LogInformation("Creating new Flight with number: {FlightNumber}", flightVM.Id);

                var flightEntity = (Flight)flightVM;
                await _unitOfWork.Repository<Flight>().AddAsync(flightEntity);
                int count = await _unitOfWork.CompleteAsync();

                if (count > 0)
                {
                    var spec = new BaseSpecification<Airplane>(a => a.Id == flightEntity.AirplaneId);
                    spec.Includes.Add(a => a.SeatTemplates);
                    var airplane = await _unitOfWork.Repository<Airplane>().GetEntityWithSpecAsync(spec);

                    var flightSeats = new List<FlightSeat>();
                    if (airplane != null && airplane.SeatTemplates != null)
                    {
                        using var transaction = await _unitOfWork.BeginTransactionAsync();
                        foreach (var seatTemplate in airplane.SeatTemplates)
                        {
                            flightSeats.Add(new FlightSeat{
                                FlightId = flightEntity.Id,
                                SeatId = seatTemplate.Id,
                                IsAvailable = true});
                        }
                        await _unitOfWork.Repository<FlightSeat>().AddRangeAsync(flightSeats);
                        await _unitOfWork.CompleteAsync();
                        await transaction.CommitAsync();
                    }

                    await InvalidateFlightCache();
                    _logger.LogInformation("Successfully created Flight: {FlightNumber} with ID: {FlightId}. Cache invalidated.",
                        flightVM.Id, flightEntity.Id);

                    try
                    {
                        await _hubContext.Clients.Group(SD.Admin).SendAsync("ReceiveUpdate");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to send real-time update to admin group");
                    }

                    TempData["success"] = "Flight has been Added Successfully";
                    return RedirectToAction(nameof(Index));
                }

                _logger.LogWarning("Failed to save Flight: {FlightNumber} to database", flightVM.Id);
                ModelState.AddModelError(string.Empty, "Failed to save Flight.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while creating Flight: {FlightNumber}", flightVM.Id);
                ModelState.AddModelError(string.Empty, "An error occurred while creating the Flight.");
            }

            await PopulateDropDownLists(airlineId: flightVM.AirlineId);
            return View(flightVM);
        }

        #endregion

        #region Edit


        [HttpGet]
        public async Task<IActionResult> Edit(int? id)
        {
            if (!id.HasValue)
            {
                _logger.LogWarning("Edit action called without ID");
                return BadRequest();
            }

            try
            {
                var cacheKey = GetFlightCacheKey(id.Value);
                var flight = await _cacheService.GetAsync<FlightViewModel>(cacheKey);

                if (flight == null)
                {
                    _logger.LogInformation("Cache miss for Flight ID: {FlightId}. Retrieving from database.", id.Value);

                    var spec = new BaseSpecification<Flight>(f => f.Id == id.Value);
                    spec.Includes.Add(f => f.Airline);
                    spec.Includes.Add(f => f.Airplane);
                    spec.Includes.Add(f => f.DepartureAirport);
                    spec.Includes.Add(f => f.ArrivalAirport);

                    var flightEntity = await _unitOfWork.Repository<Flight>().GetEntityWithSpecAsync(spec);
                    if (flightEntity == null)
                    {
                        _logger.LogWarning("Flight with ID: {FlightId} not found in database", id.Value);
                        return NotFound();
                    }

                    flight = (FlightViewModel)flightEntity;
                    await _cacheService.SetAsync(cacheKey, flight, TimeSpan.FromMinutes(20));
                    _logger.LogDebug("Cached Flight with ID: {FlightId}", id.Value);
                }
                else
                {
                    _logger.LogDebug("Cache hit for Flight ID: {FlightId}", id.Value);
                }

                await PopulateDropDownLists(airlineId: flight.AirlineId);
                _logger.LogInformation("Successfully retrieved Flight with ID: {FlightId} for editing", id.Value);
                return View(flight);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while retrieving Flight with ID: {FlightId} for edit", id.Value);
                TempData["error"] = "An error occurred while retrieving the Flight.";
                return RedirectToAction(nameof(Index));
            }
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, FlightViewModel flightVM)
        {
            if (id != flightVM.Id)
            {
                _logger.LogWarning("ID mismatch in Edit action. Route ID: {RouteId}, Model ID: {ModelId}", id, flightVM.Id);
                return BadRequest();
            }

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Edit Flight failed validation for model: {@FlightViewModel}",
                    new { flightVM.Id, flightVM.AirlineId, flightVM.DepartureAirportID });
                await PopulateDropDownLists(airlineId: flightVM.AirlineId);
                return View(flightVM);
            }

            try
            {
                _logger.LogInformation("Updating Flight with ID: {FlightId}", id);

                var existingFlight = await _unitOfWork.Repository<Flight>().GetByIdAsync(id);
                if (existingFlight == null)
                    return NotFound();
                existingFlight.AirlineId = flightVM.AirlineId;
                existingFlight.DepartureAirportID = flightVM.DepartureAirportID;
                existingFlight.ArrivalAirportID = flightVM.ArrivalAirportID;
                existingFlight.DepartureTime = flightVM.DepartureTime;
                existingFlight.ArrivalTime = flightVM.ArrivalTime;
                existingFlight.BasePrice = flightVM.BasePrice;
                existingFlight.Status = flightVM.Status;
                
                if (existingFlight.AirplaneId != flightVM.AirplaneId)
                {
                    int airplaneId = flightVM.AirplaneId;
                    int flightId = flightVM.Id;
                    existingFlight.AirplaneId = airplaneId;

                    var seatSpec = new BaseSpecification<FlightSeat>(fs => fs.FlightId == flightId);
                    var oldSeatTemplates = await _unitOfWork.Repository<FlightSeat>().GetAllWithSpecAsync(seatSpec);
                    _unitOfWork.Repository<FlightSeat>().RemoveRange(oldSeatTemplates);

                    var spec = new BaseSpecification<Airplane>(a => a.Id == airplaneId);
                    spec.Includes.Add(a => a.SeatTemplates);
                    var airplane = await _unitOfWork.Repository<Airplane>().GetEntityWithSpecAsync(spec);

                    var flightSeats = new List<FlightSeat>();
                    if (airplane != null && airplane.SeatTemplates != null)
                    {
                        foreach (var seatTemplate in airplane.SeatTemplates)
                        {
                            flightSeats.Add(new FlightSeat
                            {
                                FlightId = flightId,
                                SeatId = seatTemplate.Id,
                                IsAvailable = true
                            });
                        }
                        await _unitOfWork.Repository<FlightSeat>().AddRangeAsync(flightSeats);
                    }
                }

                _unitOfWork.Repository<Flight>().Update(existingFlight);
                await _unitOfWork.CompleteAsync();

                try
                {
                    await _hubContext.Clients.Group(SD.Admin).SendAsync("ReceiveUpdate");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send real-time update to admin group");
                }

                await InvalidateFlightCache(id);
                _logger.LogInformation("Successfully updated Flight with ID: {FlightId}. Cache invalidated.", id);

                TempData["success"] = "Flight Updated Successfully";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while updating Flight with ID: {FlightId}", id);

                if (_env.IsDevelopment())
                {
                    ModelState.AddModelError(string.Empty, ex.Message);
                    _logger.LogDebug("Development error details: {ErrorMessage}", ex.Message);
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "An Error Has Occurred during Updating the Flight");
                }
            }

            await PopulateDropDownLists(airlineId: flightVM.AirlineId);
            return View(flightVM);
        }

        #endregion

        #region Delete

        [HttpDelete]
        public async Task<IActionResult> Delete(int? id)
        {
            if (!id.HasValue)
            {
                _logger.LogWarning("Delete action called without ID");
                return Json(new { success = false, message = "Invalid ID" });
            }

            try
            {
                var flight = await _unitOfWork.Repository<Flight>().GetByIdAsync(id.Value);
                if (flight == null)
                {
                    _logger.LogWarning("Flight with ID: {FlightId} not found", id.Value);
                    return Json(new { success = false, message = "Flight not found" });
                }

                var flightSeatSpec = new BaseSpecification<FlightSeat>(fs => fs.FlightId == flight.Id);
                var flightSeat = await _unitOfWork.Repository<FlightSeat>().GetAllWithSpecAsync(flightSeatSpec);
                _unitOfWork.Repository<FlightSeat>().RemoveRange(flightSeat);
                _unitOfWork.Repository<Flight>().Delete(flight);
                await _unitOfWork.CompleteAsync();

                try
                {
                    await _hubContext.Clients.Group(SD.Admin).SendAsync("ReceiveUpdate");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send real-time update to admin group");
                }

                await InvalidateFlightCache(id.Value);
                _logger.LogInformation("Flight with ID: {FlightId} deleted successfully", id.Value);
                return Json(new { success = true, message = "Delete Successful" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while retrieving Flight with ID: {FlightId} for deletion", id.Value);
                return Json(new { success = false, message = "Error While deleting" });
            }
        }


        #endregion

        #region Details

        public async Task<IActionResult> Details(int? id)
        {
            if (!id.HasValue)
            {
                _logger.LogWarning("Details action called without ID");
                return BadRequest();
            }

            try
            {
                var cacheKey = GetFlightDetailsCacheKey(id.Value);
                var flight = await _cacheService.GetAsync<FlightViewModel>(cacheKey);

                if (flight == null)
                {
                    _logger.LogInformation("Cache miss for Flight Details ID: {FlightId}. Retrieving from database.", id.Value);
                    var spec = new BaseSpecification<Flight>(f => f.Id == id.Value);
                    spec.Includes.Add(f => f.Airline);
                    spec.Includes.Add(f => f.Airplane);
                    spec.Includes.Add(f => f.DepartureAirport);
                    spec.Includes.Add(f => f.ArrivalAirport);
                    var flightEntity = await _unitOfWork.Repository<Flight>().GetEntityWithSpecAsync(spec);
                    if (flightEntity == null)
                    {
                        _logger.LogWarning("Flight with ID: {FlightId} not found in database", id.Value);
                        return NotFound();
                    }

                    flight = (FlightViewModel)flightEntity;

                    await _cacheService.SetAsync(cacheKey, flight, TimeSpan.FromMinutes(40));
                    await _cacheService.SetAsync(GetFlightCacheKey(id.Value), flight, TimeSpan.FromMinutes(20));

                    _logger.LogDebug("Cached Flight details with ID: {FlightId}", id.Value);
                }
                else
                {
                    _logger.LogDebug("Cache hit for Flight Details ID: {FlightId}", id.Value);
                }

                _logger.LogInformation("Successfully retrieved Flight details with ID: {FlightId}", id.Value);
                return View(flight);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while retrieving Flight details with ID: {FlightId}", id.Value);
                TempData["error"] = "An error occurred while retrieving the Flight details.";
                return RedirectToAction(nameof(Index));
            }
        }

        #endregion

        #region Search

        public async Task<IActionResult> Search(string? keyword, DateTime? date)
        {
            try
            {
                _logger.LogInformation("Searching flights with keyword: {Keyword} and date: {Date}", keyword, date);

                var spec = new BaseSpecification<Flight>();
                spec.Includes.Add(f => f.Airline);
                spec.Includes.Add(f => f.Airplane);
                spec.Includes.Add(f => f.DepartureAirport);
                spec.Includes.Add(f => f.ArrivalAirport);

                if (!string.IsNullOrEmpty(keyword))
                {
                    spec.Criteria = f => (f.Airline.Name.Contains(keyword) ||
                        f.DepartureAirport.Name.Contains(keyword) ||
                        f.ArrivalAirport.Name.Contains(keyword));
                }
                if (date.HasValue)
                {
                    spec.Criteria = f => (f.DepartureTime.Date == date.Value.Date);
                }

                var flights = await _unitOfWork.Repository<Flight>().GetAllWithSpecAsync(spec);

                ViewBag.keyword = keyword;
                ViewBag.date = date;

                _logger.LogInformation("Search returned {Count} flights", flights?.Count() ?? 0);
                return View(flights?.Select(f => (FlightViewModel)f) ?? new List<FlightViewModel>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while searching flights");
                ModelState.AddModelError(string.Empty, "An error occurred while searching flights.");
                return View(new List<FlightViewModel>());
            }
        }

        #endregion

        #region GetAirplanesByAirlineId

        [HttpGet]
        public async Task<IActionResult> GetAirplanesByAirlineId(int airlineId)
        {
            try
            {
                var cacheKey = $"{CACHE_KEY_AIRPLANES_BY_AIRLINE_PREFIX}{airlineId}";

                var airplanes = await _cacheService.GetOrSetAsync<List<object>>(
                    key: cacheKey,
                    factory: async () =>
                    {
                        _logger.LogInformation("Cache miss for airplanes by flight ID: {AirlineId}", airlineId);

                        var spec = new BaseSpecification<Airplane>(e => e.AirlineId == airlineId);
                        var airplanesFromDb = await _unitOfWork.Repository<Airplane>().GetAllWithSpecAsync(spec);

                        return airplanesFromDb.Select(e => (object)new { id = e.Id, model = e.Model }).ToList();
                    },
                    expiry: TimeSpan.FromMinutes(60));

                _logger.LogDebug("Returning {Count} airplanes for flight ID: {AirlineId}", airplanes!.Count, airlineId);
                return new JsonResult(airplanes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while getting airplanes for flight ID: {AirlineId}", airlineId);
                return new JsonResult(new List<object>());
            }
        }

        #endregion

        #region Helper methods
        private string GetFlightCacheKey(int id) => $"{CACHE_KEY_FLIGHT_PREFIX}{id}";
        private string GetFlightDetailsCacheKey(int id) => $"{CACHE_KEY_FLIGHT_DETAILS_PREFIX}{id}";

        private async Task InvalidateFlightCache(int? flightId = null)
        {
            try
            {
                await _cacheService.RemoveAsync(CACHE_KEY_ALL_FLIGHTS);
                _logger.LogDebug("Removed cache for key: {CacheKey}", CACHE_KEY_ALL_FLIGHTS);

                if (flightId.HasValue)
                {
                    var tasks = new[]
                    {
                        _cacheService.RemoveAsync(GetFlightCacheKey(flightId.Value)),
                        _cacheService.RemoveAsync(GetFlightDetailsCacheKey(flightId.Value))
                    };
                    await Task.WhenAll(tasks);

                    _logger.LogDebug("Removed specific flight cache for ID: {FlightId}", flightId.Value);
                }
                else
                {
                    await _cacheService.RemoveByPatternAsync($"{CACHE_KEY_FLIGHT_PREFIX}*");
                    await _cacheService.RemoveByPatternAsync($"{CACHE_KEY_FLIGHT_DETAILS_PREFIX}*");

                    _logger.LogDebug("Removed all flight-related caches");
                }

                await _cacheService.RemoveByPatternAsync($"{CACHE_KEY_AIRPLANES_BY_AIRLINE_PREFIX}*");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating Flight cache");
            }
        }

        private async Task PopulateDropDownLists(int? airlineId = null)
        {
            try
            {
                ViewBag.Airlines = (await _unitOfWork.Repository<Airline>().GetAllAsync())
                    .Select(u => new SelectListItem
                    {
                        Text = u.Name,
                        Value = u.Id.ToString(),
                    });

                if (airlineId.HasValue)
                {
                    var cacheKey = $"{CACHE_KEY_AIRPLANES_BY_AIRLINE_PREFIX}{airlineId.Value}";
                    var cachedAirplanes = await _cacheService.GetAsync<List<Airplane>>(cacheKey);

                    if (cachedAirplanes != null && cachedAirplanes.Any())
                    {
                        ViewBag.Airplanes = cachedAirplanes.Select(u => new SelectListItem
                        {
                            Text = u.Model,
                            Value = u.Id.ToString()
                        }).ToList();
                        _logger.LogDebug("Loaded airplanes from cache for airline {AirlineId}", airlineId);
                    }
                    else
                    {
                        var spec = new BaseSpecification<Airplane>(m => m.AirlineId == airlineId.Value);
                        var airplanesFromDb = await _unitOfWork.Repository<Airplane>().GetAllWithSpecAsync(spec);

                        ViewBag.Airplanes = airplanesFromDb.Select(u => new SelectListItem
                        {
                            Text = u.Model,
                            Value = u.Id.ToString()
                        }).ToList();

                        await _cacheService.SetAsync(cacheKey, airplanesFromDb, TimeSpan.FromMinutes(60));
                        _logger.LogDebug("Loaded {Count} airplanes from DB for airline {AirlineId}", airplanesFromDb.Count(), airlineId);
                    }
                }
                else
                {
                    ViewBag.Airplanes = new List<SelectListItem>();
                }

                ViewBag.Airports = (await _unitOfWork.Repository<Airport>().GetAllAsync())
                       .Select(u => new SelectListItem
                       {
                           Text = u.Name,
                           Value = u.Id.ToString(),
                       });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while populating dropdown lists");
                ViewBag.Airlines = new List<SelectListItem>();
                ViewBag.Airplanes = new List<SelectListItem>();
                ViewBag.Airports = new List<SelectListItem>();
            }
        }

        #endregion
    }
}
