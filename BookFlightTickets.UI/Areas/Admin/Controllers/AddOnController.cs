using BookFlightTickets.Core.Domain.Entities;
using BookFlightTickets.Core.Domain.RepositoryContracts;
using BookFlightTickets.Core.ServiceContracts;
using BookFlightTickets.Core.Shared.Utility;
using BookFlightTickets.Core.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookFlightTickets.UI.Areas.Admin.Controllers
{
    [Area(SD.Admin)]
    [Authorize(Roles = SD.Admin)]
    public class AddOnController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IWebHostEnvironment _env;
        private readonly IRedisCacheService _cacheService;
        private readonly ILogger<AddOnController> _logger;

        private const string CACHE_KEY_ALL_ADDONS = "addons:all";
        private const string CACHE_KEY_ADDON_PREFIX = "addon:id:";
        private const string CACHE_PATTERN_ADDON = "addon:*";

        public AddOnController(
            IUnitOfWork unitOfWork,
            IWebHostEnvironment env,
            IRedisCacheService cacheService,
            ILogger<AddOnController> logger)
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
                _logger.LogInformation("Attempting to get all addons from cache or database");

                var addons = await _cacheService.GetOrSetAsync(
                    key: CACHE_KEY_ALL_ADDONS,
                    factory: async () =>
                    {
                        _logger.LogInformation("Cache miss for {CacheKey}. Retrieving from database.", CACHE_KEY_ALL_ADDONS);
                        var addonsFromDb = await _unitOfWork.Repository<AddOn>().GetAllAsync();
                        return addonsFromDb.Select(a => (AddOnViewModel)a).ToList();
                    },
                    expiry: TimeSpan.FromMinutes(30)
                );

                if (addons == null || !addons.Any())
                {
                    _logger.LogWarning("No addons found in cache or database");
                    ViewBag.InfoMessage = "No addons available.";
                    return View(Enumerable.Empty<AddOnViewModel>());
                }

                _logger.LogInformation("Returning {Count} addons", addons.Count());
                return View(addons);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while retrieving addons in Index action");
                ViewBag.ErrorMessage = "An error occurred while retrieving addons.";
                return View(new List<AddOnViewModel>());
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
        public async Task<IActionResult> Create(AddOnViewModel addonVM)
        {
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Create AddOn failed validation for model: {@AddOnViewModel}",
                    new { addonVM.Name, addonVM.Price });
                return View(addonVM);
            }

            try
            {
                _logger.LogInformation("Creating new AddOn with name: {AddOnName}", addonVM.Name);
                var addonEntity = (AddOn)addonVM;
                await _unitOfWork.Repository<AddOn>().AddAsync(addonEntity);
                int count = await _unitOfWork.CompleteAsync();

                if (count > 0)
                {
                    await InvalidateAllAddOnsCache();
                    _logger.LogInformation("Successfully created AddOn: {AddOnName} with ID: {AddOnId}. Cache invalidated.",
                        addonVM.Name, addonEntity.Id);

                    TempData["success"] = "AddOn has been Added Successfully";
                    return RedirectToAction(nameof(Index));
                }

                _logger.LogWarning("Failed to save AddOn: {AddOnName} to database", addonVM.Name);
                ModelState.AddModelError(string.Empty, "Failed to save AddOn.");

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while creating AddOn: {AddOnName}", addonVM.Name);
                ModelState.AddModelError(string.Empty, "An error occurred while creating the AddOn.");
            }

            return View(addonVM);
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
                var cacheKey = GetAddOnCacheKey(id.Value);
                var addon = await _cacheService.GetAsync<AddOnViewModel>(cacheKey);

                if (addon == null)
                {
                    _logger.LogInformation("Cache miss for AddOn ID: {AddOnId}. Retrieving from database.", id.Value);
                    var addonEntity = await _unitOfWork.Repository<AddOn>().GetByIdAsync(id.Value);
                    if (addonEntity == null)
                    {
                        _logger.LogWarning("AddOn with ID: {AddOnId} not found in database", id.Value);
                        return NotFound();
                    }

                    addon = (AddOnViewModel)addonEntity;
                    await _cacheService.SetAsync(cacheKey, addon, TimeSpan.FromMinutes(20));
                    _logger.LogDebug("Cached AddOn with ID: {AddOnId}", id.Value);
                }
                else
                {
                    _logger.LogDebug("Cache hit for AddOn ID: {AddOnId}", id.Value);
                }

                _logger.LogInformation("Successfully retrieved AddOn with ID: {AddOnId} for editing", id.Value);
                return View(addon);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while retrieving AddOn with ID: {AddOnId} for edit", id.Value);
                TempData["error"] = "An error occurred while retrieving the AddOn.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, AddOnViewModel addon)
        {
            if (id != addon.Id)
            {
                _logger.LogWarning("ID mismatch in Edit action. Route ID: {RouteId}, Model ID: {ModelId}", id, addon.Id);
                return BadRequest();
            }

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Edit AddOn failed validation for model: {@AddOnViewModel}",
                    new { addon.Id, addon.Name, addon.Price });
                return View(addon);
            }

            try
            {
                var existingAddon = await _unitOfWork.Repository<AddOn>().GetByIdAsync(id);
                if (existingAddon == null)
                    return NotFound();
                existingAddon.Name = addon.Name;
                existingAddon.Price = addon.Price;
                _unitOfWork.Repository<AddOn>().Update(existingAddon);
                await _unitOfWork.CompleteAsync();
                await InvalidateAddOnCache(id);
                _logger.LogInformation("Successfully updated AddOn with ID: {AddOnId}. Cache invalidated.", id);
                TempData["success"] = "AddOn Updated Successfully";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while updating AddOn with ID: {AddOnId}", id);
                if (_env.IsDevelopment())
                {
                    ModelState.AddModelError(string.Empty, ex.Message);
                    _logger.LogDebug("Development error details: {ErrorMessage}", ex.Message);
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "An Error Has Occurred during Updating the AddOn");
                }
            }
            return View(addon);
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
                var addon = await _unitOfWork.Repository<AddOn>().GetByIdAsync(id.Value);
                if (addon == null)
                {
                    _logger.LogWarning("AddOn with ID: {AddOnId} not found", id.Value);
                    return Json(new { success = false, message = "Add-On not found" });
                }

                _unitOfWork.Repository<AddOn>().Delete(addon);
                await _unitOfWork.CompleteAsync();
                await InvalidateAddOnCache(id.Value);
                _logger.LogInformation("AddOn with ID: {AddOnId} deleted successfully", id.Value);
                return Json(new { success = true, message = "Delete Successful" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while retrieving AddOn with ID: {AddOnId} for deletion", id.Value);
                return Json(new { success = false, message = "Error While deleting" });
            }
        }

        #endregion

        #region Helper methods

        private async Task InvalidateAddOnCache(int addonId)
        {
            try
            {
                var tasks = new[]
                {
                    _cacheService.RemoveAsync(CACHE_KEY_ALL_ADDONS),
                    _cacheService.RemoveAsync(GetAddOnCacheKey(addonId))
                };
                await Task.WhenAll(tasks);
                _logger.LogInformation("Cache invalidated for AddOn {Id}", addonId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cache invalidation failed for AddOn {Id}", addonId);
            }
        }

        private async Task InvalidateAllAddOnsCache()
        {
            try
            {
                await _cacheService.RemoveAsync(CACHE_KEY_ALL_ADDONS);
                _logger.LogInformation("All AddOns cache invalidated");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cache invalidation failed for all AddOns");
            }
        }

        private string GetAddOnCacheKey(int id) => $"{CACHE_KEY_ADDON_PREFIX}{id}";

        #endregion
    }
}
