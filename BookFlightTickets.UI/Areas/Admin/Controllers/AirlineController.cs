using BookFlightTickets.Core.Domain.Entities;
using BookFlightTickets.Core.Domain.Hubs;
using BookFlightTickets.Core.Domain.RepositoryContracts;
using BookFlightTickets.Core.ServiceContracts;
using BookFlightTickets.Core.Shared.Utility;
using BookFlightTickets.Core.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
namespace BookFlightTickets.UI.Areas.Admin.Controllers
{
    [Area(SD.Admin)]
    [Authorize(Roles = SD.Admin)]
    public class AirlineController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IWebHostEnvironment _env;
        private readonly IRedisCacheService _cacheService;
        private readonly ILogger<AirlineController> _logger;
        private readonly IHubContext<DashboardHub> _hubContext;
        private const string CACHE_KEY_ALL_AIRLINES = "airlines:all";
        private const string CACHE_KEY_AIRLINE_PREFIX = "airline:id:";
        private const string CACHE_KEY_AIRLINE_DETAILS_PREFIX = "airline:details:id:";

        public AirlineController(
            IUnitOfWork unitOfWork,
            IWebHostEnvironment env,
            IRedisCacheService cacheService,
            ILogger<AirlineController> logger,
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
                _logger.LogInformation("Attempting to get all airlines from cache or database");
                var airlines = await _cacheService.GetOrSetAsync(
                   key: CACHE_KEY_ALL_AIRLINES,
                   factory: async () =>
                   {
                       _logger.LogInformation("Cache miss for {CacheKey}. Retrieving from database.", CACHE_KEY_ALL_AIRLINES);
                       var airlinesFromDb = await _unitOfWork.Repository<Airline>().GetAllAsync();
                       return airlinesFromDb.Select(a => (AirlineViewModel)a).ToList();
                   },
                   expiry: TimeSpan.FromMinutes(30)
               );

                if (airlines == null || !airlines.Any())
                {
                    _logger.LogWarning("No airlines found in cache or database");
                    ViewBag.InfoMessage = "No airlines available.";
                    return View(Enumerable.Empty<AirlineViewModel>());
                }

                _logger.LogInformation("Returning {Count} airlines", airlines.Count());
                return View(airlines);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while retrieving airlines in Index action");
                ViewBag.ErrorMessage = "An error occurred while retrieving airlines.";
                return View(new List<AirlineViewModel>());
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
        public async Task<IActionResult> Create(AirlineViewModel airlineVM)
        {
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Create Airline failed validation for model: {@AirlineViewModel}",
                    new { airlineVM.Name, airlineVM.Code });
                return View(airlineVM);
            }

            try
            {
                _logger.LogInformation("Creating new Airline with name: {AirlineName}", airlineVM.Name);
                var airlineEntity = (Airline)airlineVM;
                await _unitOfWork.Repository<Airline>().AddAsync(airlineEntity);
                int count = await _unitOfWork.CompleteAsync();

                if (count > 0)
                {
                    await InvalidateAirlineCache();
                    _logger.LogInformation("Successfully created Airline: {AirlineName} with ID: {AirlineId}. Cache invalidated.",
                        airlineVM.Name, airlineEntity.Id);

                    try
                    {
                        await _hubContext.Clients.Group(SD.Admin).SendAsync("ReceiveUpdate");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to send real-time update to admin group");
                    }

                    TempData["success"] = "Airline has been Added Successfully";
                    return RedirectToAction(nameof(Index));
                }

                _logger.LogWarning("Failed to save Airline: {AirlineName} to database", airlineVM.Name);
                ModelState.AddModelError(string.Empty, "Failed to save Airline.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while creating Airline: {AirlineName}", airlineVM.Name);
                ModelState.AddModelError(string.Empty, "An error occurred while creating the Airline.");
            }

            return View(airlineVM);
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
                var cacheKey = GetAirlineCacheKey(id.Value);
                var airline = await _cacheService.GetAsync<AirlineViewModel>(cacheKey);

                if (airline == null)
                {
                    _logger.LogInformation("Cache miss for Airline ID: {AirlineId}. Retrieving from database.", id.Value);
                    var airlineEntity = await _unitOfWork.Repository<Airline>().GetByIdAsync(id.Value);
                    if (airlineEntity == null)
                    {
                        _logger.LogWarning("Airline with ID: {AirlineId} not found in database", id.Value);
                        return NotFound();
                    }

                    airline = (AirlineViewModel)airlineEntity;
                    await _cacheService.SetAsync(cacheKey, airline, TimeSpan.FromMinutes(20));
                    _logger.LogDebug("Cached Airline with ID: {AirlineId}", id.Value);
                }
                else
                {
                    _logger.LogDebug("Cache hit for Airline ID: {AirlineId}", id.Value);
                }

                _logger.LogInformation("Successfully retrieved Airline with ID: {AirlineId} for editing", id.Value);
                return View(airline);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while retrieving Airline with ID: {AirlineId} for edit", id.Value);
                TempData["error"] = "An error occurred while retrieving the Airline.";
                return RedirectToAction(nameof(Index));
            }
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, AirlineViewModel airlineVM)
        {
            if (id != airlineVM.Id)
            {
                _logger.LogWarning("ID mismatch in Edit action. Route ID: {RouteId}, Model ID: {ModelId}", id, airlineVM.Id);
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Edit Airline failed validation for model: {@AirlineViewModel}",
                    new { airlineVM.Id, airlineVM.Name, airlineVM.Code });
                return View(airlineVM);
            }

            try
            {
                var existingAirline = await _unitOfWork.Repository<Airline>().GetByIdAsync(id);
                if (existingAirline == null)
                    return NotFound();
                existingAirline.Name = airlineVM.Name;
                existingAirline.Code = airlineVM.Code;
                _unitOfWork.Repository<Airline>().Update(existingAirline);
                await _unitOfWork.CompleteAsync();

                try
                {
                    await _hubContext.Clients.Group(SD.Admin).SendAsync("ReceiveUpdate");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send real-time update to admin group");
                }

                await InvalidateAirlineCache(id);
                _logger.LogInformation("Successfully updated Airline with ID: {AirlineId}. Cache invalidated.", id);
                TempData["success"] = "Airline Updated Successfully";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while updating Airline with ID: {AirlineId}", id);
                if (_env.IsDevelopment())
                {
                    ModelState.AddModelError(string.Empty, ex.Message);
                    _logger.LogDebug("Development error details: {ErrorMessage}", ex.Message);
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "An Error Has Occurred during Updating the Airline");
                }
            }

            return View(airlineVM);
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
                var cacheKey = GetAirlineDetailsCacheKey(id.Value);
                var airline = await _cacheService.GetAsync<AirlineViewModel>(cacheKey);

                if (airline == null)
                {
                    _logger.LogInformation("Cache miss for Airline Details ID: {AirlineId}. Retrieving from database.", id.Value);
                    var airlineEntity = await _unitOfWork.Repository<Airline>().GetByIdAsync(id.Value);
                    if (airlineEntity == null)
                    {
                        _logger.LogWarning("Airline with ID: {AirlineId} not found in database", id.Value);
                        return NotFound();
                    }

                    airline = (AirlineViewModel)airlineEntity;

                    await _cacheService.SetAsync(cacheKey, airline, TimeSpan.FromMinutes(40));
                    await _cacheService.SetAsync(GetAirlineCacheKey(id.Value), airline, TimeSpan.FromMinutes(20));

                    _logger.LogDebug("Cached Airline details with ID: {AirlineId}", id.Value);
                }
                else
                {
                    _logger.LogDebug("Cache hit for Airline Details ID: {AirlineId}", id.Value);
                }

                _logger.LogInformation("Successfully retrieved Airline details with ID: {AirlineId}", id.Value);
                return View(airline);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while retrieving Airline details with ID: {AirlineId}", id.Value);
                TempData["error"] = "An error occurred while retrieving the Airline details.";
                return RedirectToAction(nameof(Index));
            }
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
                var airline = await _unitOfWork.Repository<Airline>().GetByIdAsync(id.Value);
                if (airline == null)
                {
                    _logger.LogWarning("Airline with ID: {AirlineId} not found", id.Value);
                    return Json(new { success = false, message = "Airline not found" });

                }

                _unitOfWork.Repository<Airline>().Delete(airline);
                await _unitOfWork.CompleteAsync();

                try
                {
                    await _hubContext.Clients.Group(SD.Admin).SendAsync("ReceiveUpdate");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send real-time update to admin group");
                }

                await InvalidateAirlineCache(id.Value);
                _logger.LogInformation("Airline with ID: {AirlineId} deleted successfully", id.Value);
                return Json(new { success = true, message = "Delete Successful" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while retrieving Airline with ID: {AirlineId} for deletion", id.Value);
                return Json(new { success = false, message = "Error While deleting" });
            }
        }

        #endregion

        #region Helper methods

        private string GetAirlineCacheKey(int id) => $"{CACHE_KEY_AIRLINE_PREFIX}{id}";
        private string GetAirlineDetailsCacheKey(int id) => $"{CACHE_KEY_AIRLINE_DETAILS_PREFIX}{id}";

        private async Task InvalidateAirlineCache(int? airlineId = null)
        {
            try
            {
                await _cacheService.RemoveAsync(CACHE_KEY_ALL_AIRLINES);
                await _cacheService.RemoveAsync("airlines:dropdown");
                _logger.LogDebug("Removed cache for key: {CacheKey}", CACHE_KEY_ALL_AIRLINES);

                if (airlineId.HasValue)
                {
                    var tasks = new[]
                    {
                        _cacheService.RemoveAsync(GetAirlineCacheKey(airlineId.Value)),
                        _cacheService.RemoveAsync(GetAirlineDetailsCacheKey(airlineId.Value))
                    };
                    await Task.WhenAll(tasks);

                    _logger.LogDebug("Removed specific airline cache for ID: {AirlineId}", airlineId.Value);
                }
                else
                {
                    await _cacheService.RemoveByPatternAsync($"{CACHE_KEY_AIRLINE_PREFIX}*");
                    await _cacheService.RemoveByPatternAsync($"{CACHE_KEY_AIRLINE_DETAILS_PREFIX}*");

                    _logger.LogDebug("Removed all airline-related caches");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating Airline cache");
            }
        }

        #endregion
    }
}
