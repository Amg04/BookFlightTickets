using BookFlightTickets.Core.Domain.Entities;
using BookFlightTickets.Core.Domain.RepositoryContracts;
using BookFlightTickets.Core.Domain.Specifications;
using BookFlightTickets.Core.ServiceContracts;
using BookFlightTickets.Core.Shared.Utility;
using BookFlightTickets.Core.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace BookFlightTickets.UI.Areas.Admin.Controllers
{
    [Area(SD.Admin)]
    [Authorize(Roles = SD.Admin)]
    public class AirportController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IWebHostEnvironment _env;
        private readonly IRedisCacheService _cacheService;
        private readonly ILogger<AirportController> _logger;

        private const string CACHE_KEY_ALL_AIRPORTS = "airports:all";
        private const string CACHE_KEY_AIRPORT_PREFIX = "airport:id:";
        private const string CACHE_KEY_AIRPORT_DETAILS_PREFIX = "airport:details:id:";

        public AirportController(
            IUnitOfWork unitOfWork,
            IWebHostEnvironment env,
             IRedisCacheService cacheService,
            ILogger<AirportController> logger)
        {
            _unitOfWork = unitOfWork;
            _env = env;
            _cacheService = cacheService;
            _logger = logger;
        }

        #region Index

        public async Task<IActionResult> Index()
        {
            try
            {
                _logger.LogInformation("Attempting to get all airports from cache or database");

                var airports = await _cacheService.GetOrSetAsync(
                key: CACHE_KEY_ALL_AIRPORTS,
                factory: async () =>
                {
                    _logger.LogInformation("Cache miss for {CacheKey}. Retrieving from database.", CACHE_KEY_ALL_AIRPORTS);

                    var spec = new BaseSpecification<Airport>();
                    var airportsFromDb = await _unitOfWork.Repository<Airport>().GetAllWithSpecAsync(spec);

                    if (airportsFromDb == null)
                    {
                        _logger.LogWarning("Database returned null for airports");
                        return new List<AirportViewModel>();
                    }

                    return airportsFromDb.Select(a => (AirportViewModel)a).ToList();
                },
                expiry: TimeSpan.FromMinutes(30));

                if (airports == null || !airports.Any())
                {
                    _logger.LogWarning("No airports found in cache or database. Cache key: {CacheKey}", CACHE_KEY_ALL_AIRPORTS);
                    ViewBag.InfoMessage = "No airports available.";
                    return View(Enumerable.Empty<AirportViewModel>());

                }

                _logger.LogInformation("Returning {Count} airports", airports.Count());
                return View(airports);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while retrieving airports in Index action");
                ViewBag.ErrorMessage = "An error occurred while retrieving airports.";
                return View(new List<AirportViewModel>());
            }
        }

        #endregion

        #region Create

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(AirportViewModel airportVM)
        {
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Create Airport failed validation for model: {@AirportViewModel}",
                    new { airportVM.Name, airportVM.Code, airportVM.City, airportVM.Country });
                return View(airportVM);
            }

            try
            {
                _logger.LogInformation("Creating new Airport with name: {AirportName}", airportVM.Name);

                var airportEntity = (Airport)airportVM;
                await _unitOfWork.Repository<Airport>().AddAsync(airportEntity);
                int count = await _unitOfWork.CompleteAsync();

                if (count > 0)
                {
                    await InvalidateAirportCache();
                    _logger.LogInformation("Successfully created Airport: {AirportName} with ID: {AirportId}. Cache invalidated.",
                        airportVM.Name, airportEntity.Id);

                    TempData["success"] = "Airport has been Added Successfully";
                    return RedirectToAction(nameof(Index));
                }

                _logger.LogWarning("Failed to save Airport: {AirportName} to database", airportVM.Name);
                ModelState.AddModelError(string.Empty, "Failed to save Airport.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while creating Airport: {AirportName}", airportVM.Name);
                TempData["error"] = "An error occurred while creating the Airport.";
                return RedirectToAction(nameof(Index));

            }

            return View(airportVM);
        }

        #endregion

        #region Edit

        public async Task<IActionResult> Edit(int? id)
        {
            if (!id.HasValue)
            {
                _logger.LogWarning("Edit action called without ID");
                return BadRequest();
            }

            try
            {
                var cacheKey = GetAirportCacheKey(id.Value);
                var airport = await _cacheService.GetAsync<AirportViewModel>(cacheKey);

                if (airport == null)
                {
                    _logger.LogInformation("Cache miss for Airport ID: {AirportId}. Retrieving from database.", id.Value);

                    var airportEntity = await _unitOfWork.Repository<Airport>().GetByIdAsync(id.Value);
                    if (airportEntity == null)
                    {
                        _logger.LogWarning("Airport with ID: {AirportId} not found in database", id.Value);
                        return NotFound();
                    }

                    airport = (AirportViewModel)airportEntity;
                    await _cacheService.SetAsync(cacheKey, airport, TimeSpan.FromMinutes(20));
                    _logger.LogDebug("Cached Airport with ID: {AirportId}", id.Value);
                }
                else
                {
                    _logger.LogDebug("Cache hit for Airport ID: {AirportId}", id.Value);
                }

                _logger.LogInformation("Successfully retrieved Airport with ID: {AirportId} for editing", id.Value);
                return View(airport);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while retrieving Airport with ID: {AirportId} for edit", id.Value);
                TempData["error"] = "An error occurred while retrieving the Airport.";
                return RedirectToAction(nameof(Index));
            }
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, AirportViewModel airportVM)
        {
            if (id != airportVM.Id)
            {
                _logger.LogWarning("ID mismatch in Edit action. Route ID: {RouteId}, Model ID: {ModelId}", id, airportVM.Id);
                return BadRequest();
            }

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Edit Airport failed validation for model: {@AirportViewModel}",
                    new { airportVM.Id, airportVM.Name, airportVM.Code, airportVM.City });
                return View(airportVM);
            }

            try
            {
                _logger.LogInformation("Updating Airport with ID: {AirportId}", id);

                var existingAirport = await _unitOfWork.Repository<Airport>().GetByIdAsync(id);
                if (existingAirport == null)
                    return NotFound();
                existingAirport.Name = airportVM.Name;
                existingAirport.Code = airportVM.Code;
                existingAirport.City = airportVM.City;
                existingAirport.Country = airportVM.Country;
                _unitOfWork.Repository<Airport>().Update(existingAirport);
                await _unitOfWork.CompleteAsync();

                await InvalidateAirportCache(id);
                _logger.LogInformation("Successfully updated Airport with ID: {AirportId}. Cache invalidated.", id);

                TempData["success"] = "Airport Updated Successfully";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while updating Airport with ID: {AirportId}", id);

                if (_env.IsDevelopment())
                {
                    ModelState.AddModelError(string.Empty, ex.Message);
                    _logger.LogDebug("Development error details: {ErrorMessage}", ex.Message);
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "An Error Has Occurred during Updating the Airport");
                }
            }

            return View(airportVM);
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
                var airport = await _unitOfWork.Repository<Airport>().GetByIdAsync(id.Value);
                if (airport == null)
                {
                    _logger.LogWarning("Airport with ID: {AddOnId} not found", id.Value);
                    return Json(new { success = false, message = "Airport not found" });
                }

                _unitOfWork.Repository<Airport>().Delete(airport);
                await _unitOfWork.CompleteAsync();
                await InvalidateAirportCache(id.Value);
                _logger.LogInformation("Airport with ID: {AddOnId} deleted successfully", id.Value);
                return Json(new { success = true, message = "Delete Successful" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while retrieving Airport with ID: {AirportId} for deletion", id.Value);
                return Json(new { success = false, message = "Error While deleting" });
            }
        }

        #endregion

        #region Helper methods

        private string GetAirportCacheKey(int id) => $"{CACHE_KEY_AIRPORT_PREFIX}{id}";
        private string GetAirportDetailsCacheKey(int id) => $"{CACHE_KEY_AIRPORT_DETAILS_PREFIX}{id}";

        private async Task InvalidateAirportCache(int? airportId = null)
        {
            try
            {
                await _cacheService.RemoveAsync(CACHE_KEY_ALL_AIRPORTS);
                _logger.LogDebug("Removed cache for key: {CacheKey}", CACHE_KEY_ALL_AIRPORTS);

                if (airportId.HasValue)
                {
                    var tasks = new[]
                    {
                        _cacheService.RemoveAsync(GetAirportCacheKey(airportId.Value)),
                        _cacheService.RemoveAsync(GetAirportDetailsCacheKey(airportId.Value))
                    };
                    await Task.WhenAll(tasks);

                    _logger.LogDebug("Removed specific airport cache for ID: {AirportId}", airportId.Value);
                }
                else
                {
                    await _cacheService.RemoveByPatternAsync($"{CACHE_KEY_AIRPORT_PREFIX}*");
                    await _cacheService.RemoveByPatternAsync($"{CACHE_KEY_AIRPORT_DETAILS_PREFIX}*");

                    _logger.LogDebug("Removed all airport-related caches");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating Airport cache");
            }
        }

        #endregion
    }
}
