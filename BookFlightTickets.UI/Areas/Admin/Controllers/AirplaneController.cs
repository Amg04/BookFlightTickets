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
    public class AirplaneController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IWebHostEnvironment _env;
        private readonly IRedisCacheService _cacheService;
        private readonly ILogger<AirplaneController> _logger;
        private readonly IHubContext<DashboardHub> _hubContext;
        private const string CACHE_KEY_ALL_AIRPLANES = "airplanes:all";
        private const string CACHE_KEY_AIRPLANE_PREFIX = "airplane:id:";
        private const string CACHE_KEY_AIRLINES_DROPDOWN = "airlines:dropdown";
        private const string CACHE_KEY_AIRPLANE_WITH_AIRLINE_PREFIX = "airplane:with-airline:id:";

        public AirplaneController(
            IUnitOfWork unitOfWork,
            IWebHostEnvironment env,
            IRedisCacheService cacheService,
            ILogger<AirplaneController> logger,
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
                _logger.LogInformation("Attempting to get all airplanes from cache or database");

                var airplanes = await _cacheService.GetOrSetAsync(
                    key: CACHE_KEY_ALL_AIRPLANES,
                    factory: async () =>
                    {
                        _logger.LogInformation("Cache miss for {CacheKey}. Retrieving from database.", CACHE_KEY_ALL_AIRPLANES);

                        var spec = new BaseSpecification<Airplane>();
                        spec.Includes.Add(f => f.Airline);
                        var airplanesFromDb = await _unitOfWork.Repository<Airplane>().GetAllWithSpecAsync(spec);

                        return airplanesFromDb.Select(a => (AirplaneViewModel)a).ToList();
                    },
                    expiry: TimeSpan.FromMinutes(30)
                );

                if (airplanes == null || !airplanes.Any())
                {
                    _logger.LogWarning("No airplanes found in cache or database");
                    ViewBag.InfoMessage = "No airplanes available.";
                    return View(Enumerable.Empty<AirplaneViewModel>());
                }

                _logger.LogInformation("Returning {Count} airplanes", airplanes.Count());
                return View(airplanes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while retrieving airplanes in Index action");
                ViewBag.ErrorMessage = "An error occurred while retrieving airplanes.";
                return View(new List<AirplaneViewModel>());
            }
        }

        #endregion

        #region Create

        public async Task<IActionResult> Create()
        {
            try
            {
                var airlines = await GetAirlinesForDropdownAsync();
                ViewBag.Airlines = airlines;
                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while loading airlines dropdown for Create");
                TempData["error"] = "An error occurred while loading airlines.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(AirplaneViewModel airplaneVM)
        {
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Create Airplane failed validation for model: {@AirplaneViewModel}",
                    new { airplaneVM.Model, airplaneVM.SeatCapacity, airplaneVM.AirlineId });

                await LoadAirlinesDropdownAsync();
                return View(airplaneVM);
            }

            try
            {
                _logger.LogInformation("Creating new Airplane with model: {AirplaneModel}", airplaneVM.Model);

                var airplaneEntity = (Airplane)airplaneVM;
                await _unitOfWork.Repository<Airplane>().AddAsync(airplaneEntity);
                int count = await _unitOfWork.CompleteAsync();

                if (count > 0)
                {
                    await InvalidateAirplaneCache();
                    string airlineCacheKey = $"airplanes:flight:{airplaneVM.AirlineId}";
                    await _cacheService.RemoveAsync(airlineCacheKey);
                    _logger.LogInformation("Successfully created Airplane: {AirplaneModel} with ID: {AirplaneId}. Cache invalidated.",
                        airplaneVM.Model, airplaneEntity.Id);

                    try
                    {
                        await _hubContext.Clients.Group(SD.Admin).SendAsync("ReceiveUpdate");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to send real-time update to admin group");
                    }

                    TempData["success"] = "Airplane has been Added Successfully";
                    return RedirectToAction(nameof(Index));
                }

                _logger.LogWarning("Failed to save Airplane: {AirplaneModel} to database", airplaneVM.Model);
                ModelState.AddModelError(string.Empty, "Failed to save Airplane.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while creating Airplane: {AirplaneModel}", airplaneVM.Model);
                ModelState.AddModelError(string.Empty, "An error occurred while creating the Airplane.");
            }

            await LoadAirlinesDropdownAsync();
            return View(airplaneVM);
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
                var cacheKey = GetAirplaneWithAirlineCacheKey(id.Value);
                var airplane = await _cacheService.GetAsync<AirplaneViewModel>(cacheKey);

                if (airplane == null)
                {
                    _logger.LogInformation("Cache miss for Airplane ID: {AirplaneId}. Retrieving from database.", id.Value);

                    var airplaneEntity = await _unitOfWork.Repository<Airplane>().GetByIdAsync(id.Value);
                    if (airplaneEntity == null)
                    {
                        _logger.LogWarning("Airplane with ID: {AirplaneId} not found in database", id.Value);
                        return NotFound();
                    }

                    airplane = (AirplaneViewModel)airplaneEntity;

                    var spec = new BaseSpecification<Airplane>(a => a.Id == id.Value);
                    spec.Includes.Add(f => f.Airline);

                    var airplaneWithAirline = await _unitOfWork.Repository<Airplane>().GetEntityWithSpecAsync(spec);
                    if (airplaneWithAirline != null)
                    {
                        var detailedViewModel = (AirplaneViewModel)airplaneWithAirline;
                        await _cacheService.SetAsync(cacheKey, detailedViewModel, TimeSpan.FromMinutes(20));
                        _logger.LogDebug("Cached Airplane with Airline details for ID: {AirplaneId}", id.Value);
                    }
                }
                else
                {
                    _logger.LogDebug("Cache hit for Airplane ID: {AirplaneId}", id.Value);
                }

                var airlines = await GetAirlinesForDropdownAsync();
                ViewBag.Airlines = airlines;

                _logger.LogInformation("Successfully retrieved Airplane with ID: {AirplaneId} for editing", id.Value);
                return View(airplane);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while retrieving Airplane with ID: {AirplaneId} for edit", id.Value);
                TempData["error"] = "An error occurred while retrieving the Airplane.";
                return RedirectToAction(nameof(Index));
            }
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, AirplaneViewModel airplaneVM)
        {
            if (id != airplaneVM.Id)
            {
                _logger.LogWarning("ID mismatch in Edit action. Route ID: {RouteId}, Model ID: {ModelId}", id, airplaneVM.Id);
                return BadRequest();
            }

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Edit Airplane failed validation for model: {@AirplaneViewModel}",
                    new { airplaneVM.Id, airplaneVM.Model, airplaneVM.SeatCapacity, airplaneVM.AirlineId });

                await LoadAirlinesDropdownAsync();
                return View(airplaneVM);
            }

            try
            {
                _logger.LogInformation("Updating Airplane with ID: {AirplaneId}", id);

                var existingAirplane = await _unitOfWork.Repository<Airplane>().GetByIdAsync(id);
                if (existingAirplane == null)
                    return NotFound();
                int oldAirlineId = existingAirplane.AirlineId;
                existingAirplane.AirlineId = airplaneVM.AirlineId;
                existingAirplane.Model = airplaneVM.Model;
                existingAirplane.SeatCapacity = airplaneVM.SeatCapacity;
                _unitOfWork.Repository<Airplane>().Update(existingAirplane);
                await _unitOfWork.CompleteAsync();

                await InvalidateAirplaneCache(id);
                await _cacheService.RemoveAsync($"airplanes:flight:{oldAirlineId}");
                await _cacheService.RemoveAsync($"airlines:dropdown");
                if (oldAirlineId != airplaneVM.AirlineId)
                    await _cacheService.RemoveAsync($"airplanes:flight:{airplaneVM.AirlineId}");
                _logger.LogInformation("Successfully updated Airplane with ID: {AirplaneId}. Cache invalidated.", id);

                TempData["success"] = "Airplane Updated Successfully";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while updating Airplane with ID: {AirplaneId}", id);
                ModelState.AddModelError(string.Empty, "An error occurred while updating the Airplane.");
            }

            await LoadAirlinesDropdownAsync();
            return View(airplaneVM);
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
                var airplane = await _unitOfWork.Repository<Airplane>().GetByIdAsync(id.Value);
                if (airplane == null)
                {
                    _logger.LogWarning("Airplane with ID: {AirplaneId} not found", id.Value);
                    return Json(new { success = false, message = "Airplane not found" });
                }
                int airlineId = airplane.AirlineId;

                _unitOfWork.Repository<Airplane>().Delete(airplane);
                await _unitOfWork.CompleteAsync();

                try
                {
                    await _hubContext.Clients.Group(SD.Admin).SendAsync("ReceiveUpdate");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send real-time update to admin group");
                }

                await InvalidateAirplaneCache(id.Value);
                await _cacheService.RemoveAsync($"airplanes:flight:{airlineId}");
                _logger.LogInformation("Airplane with ID: {AirplaneId} deleted successfully", id.Value);
                return Json(new { success = true, message = "Delete Successful" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while retrieving Airplane with ID: {AirplaneId} for deletion", id.Value);
                return Json(new { success = false, message = "Error While deleting" });
            }
        }

        #endregion

        #region Helper methods

        private string GetAirplaneCacheKey(int id) => $"{CACHE_KEY_AIRPLANE_PREFIX}{id}";
        private string GetAirplaneWithAirlineCacheKey(int id) => $"{CACHE_KEY_AIRPLANE_WITH_AIRLINE_PREFIX}{id}";

        private async Task<List<SelectListItem>?> GetAirlinesForDropdownAsync()
        {
            return await _cacheService.GetOrSetAsync(
                key: CACHE_KEY_AIRLINES_DROPDOWN,
                factory: async () =>
                {
                    _logger.LogDebug("Cache miss for airlines dropdown. Retrieving from database.");

                    var airlines = await _unitOfWork.Repository<Airline>().GetAllAsync();
                    return airlines.Select(a => new SelectListItem
                    {
                        Text = a.Name,
                        Value = a.Id.ToString(),
                    }).ToList();
                },
                expiry: TimeSpan.FromHours(1)
            );
        }

        private async Task LoadAirlinesDropdownAsync()
        {
            try
            {
                var airlines = await GetAirlinesForDropdownAsync();
                ViewBag.Airlines = airlines;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading airlines dropdown");
                ViewBag.Airlines = new List<SelectListItem>();
            }
        }

        private async Task InvalidateAirplaneCache(int? airplaneId = null)
        {
            try
            {
                await _cacheService.RemoveAsync(CACHE_KEY_ALL_AIRPLANES);
                _logger.LogDebug("Removed cache for key: {CacheKey}", CACHE_KEY_ALL_AIRPLANES);

                if (airplaneId.HasValue)
                {
                    var tasks = new[]
                    {
                        _cacheService.RemoveAsync(GetAirplaneCacheKey(airplaneId.Value)),
                        _cacheService.RemoveAsync(GetAirplaneWithAirlineCacheKey(airplaneId.Value))
                    };
                    await Task.WhenAll(tasks);

                    _logger.LogDebug("Removed specific airplane cache for ID: {AirplaneId}", airplaneId.Value);
                }
                else
                {
                    await _cacheService.RemoveByPatternAsync($"{CACHE_KEY_AIRPLANE_PREFIX}*");
                    await _cacheService.RemoveByPatternAsync($"{CACHE_KEY_AIRPLANE_WITH_AIRLINE_PREFIX}*");

                    _logger.LogDebug("Removed all airplane-related caches");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating Airplane cache");
            }
        }

        #endregion
    }
}
