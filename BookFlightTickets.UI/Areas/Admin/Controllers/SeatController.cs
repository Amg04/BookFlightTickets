using BookFlightTickets.Core.Domain.Entities;
using BookFlightTickets.Core.Domain.RepositoryContracts;
using BookFlightTickets.Core.Domain.Specifications;
using BookFlightTickets.Core.ServiceContracts;
using BookFlightTickets.Core.Shared.Utility;
using BookFlightTickets.Core.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
namespace BookFlightTickets.UI.Areas.Admin.Controllers
{
    [Area(SD.Admin)]
    [Authorize(Roles = SD.Admin)]
    public class SeatController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IWebHostEnvironment _env;
        private readonly IRedisCacheService _cacheService;
        private readonly ILogger<SeatController> _logger;

        private const string CACHE_KEY_ALL_SEATS = "seats:all";
        private const string CACHE_KEY_SEAT_PREFIX = "seat:id:";
        private const string CACHE_KEY_SEAT_DETAILS_PREFIX = "seat:details:id:";

        public SeatController(
            IUnitOfWork unitOfWork, 
            IWebHostEnvironment env,
            IRedisCacheService cacheService,
            ILogger<SeatController> logger)
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
                _logger.LogInformation("Attempting to get all seats from cache or database");
                var seats = await _cacheService.GetOrSetAsync(
                    key: CACHE_KEY_ALL_SEATS,
                    factory: async () =>
                    {
                        _logger.LogInformation("Cache miss for {CacheKey}. Retrieving from database.", CACHE_KEY_ALL_SEATS);
                        var spec = new BaseSpecification<Seat>();
                        spec.Includes.Add(s => s.Airplane);
                        var seatsFromDb = await _unitOfWork.Repository<Seat>().GetAllWithSpecAsync(spec);
                        return seatsFromDb.Select(s => (SeatViewModel)s).ToList();
                    },
                    expiry: TimeSpan.FromMinutes(30)
                );

                if (seats == null || !seats.Any())
                {
                    _logger.LogWarning("No seats found in cache or database");
                    ViewBag.InfoMessage = "No seats available.";
                    return View(Enumerable.Empty<SeatViewModel>());
                }

                _logger.LogInformation("Returning {Count} seats", seats.Count());
                return View(seats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while retrieving seats in Index action");
                ViewBag.ErrorMessage = "An error occurred while retrieving seats.";
                return View(new List<SeatViewModel>());
            }
        }

        #endregion

        #region Create

        public async Task<IActionResult> Create()
        {
            try
            {
                _logger.LogDebug("Loading airplanes and seat classes for Create view");
                await LoadViewBagsAsync();
                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading Create view");
                TempData["error"] = "An error occurred while loading the form.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SeatViewModel seatVM)
        {
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Create Seat failed validation for model: {@SeatViewModel}",
                    new { seatVM.AirplaneId, seatVM.Row, seatVM.Number });
                await LoadViewBagsAsync();
                return View(seatVM);
            }

            try
            {
                _logger.LogInformation("Creating new seat for Airplane ID: {AirplaneId}", seatVM.AirplaneId);

                var checkAvailableSeat = await _unitOfWork.Repository<Seat>()
                    .CountAsync(new BaseSpecification<Seat>(a => a.AirplaneId == seatVM.AirplaneId));

                var airplane = await _unitOfWork.Repository<Airplane>().GetByIdAsync(seatVM.AirplaneId);

                if (airplane == null)
                {
                    _logger.LogWarning("Airplane with ID: {AirplaneId} not found", seatVM.AirplaneId);
                    return NotFound();
                }

                if (checkAvailableSeat >= airplane.SeatCapacity)
                {
                    _logger.LogWarning("Seat capacity full for Airplane ID: {AirplaneId}", seatVM.AirplaneId);
                    TempData["error"] = "The seat capacity is full. You cannot add more seats.";
                    return RedirectToAction(nameof(Index));
                }

                var seatEntity = (Seat)seatVM;
                await _unitOfWork.Repository<Seat>().AddAsync(seatEntity);
                int count = await _unitOfWork.CompleteAsync();

                if (count > 0)
                {
                    await InvalidateSeatCache();
                    _logger.LogInformation("Successfully created Seat ID: {SeatId} for Airplane ID: {AirplaneId}. Cache invalidated.",
                        seatEntity.Id, seatVM.AirplaneId);

                    TempData["success"] = "Seat has been Added Successfully";
                    return RedirectToAction(nameof(Index));
                }

                _logger.LogWarning("Failed to save Seat to database");
                ModelState.AddModelError(string.Empty, "Failed to save Seat.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while creating Seat");
                ModelState.AddModelError(string.Empty, "An error occurred while creating the Seat.");
            }

            await LoadViewBagsAsync();
            return View(seatVM);
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
                var cacheKey = GetSeatCacheKey(id.Value);
                var seat = await _cacheService.GetAsync<SeatViewModel>(cacheKey);

                if (seat == null)
                {
                    _logger.LogInformation("Cache miss for Seat ID: {SeatId}. Retrieving from database.", id.Value);
                    var seatEntity = await _unitOfWork.Repository<Seat>().GetByIdAsync(id.Value);
                    if (seatEntity == null)
                    {
                        _logger.LogWarning("Seat with ID: {SeatId} not found in database", id.Value);
                        return NotFound();
                    }

                    seat = (SeatViewModel)seatEntity;
                    await _cacheService.SetAsync(cacheKey, seat, TimeSpan.FromMinutes(20));
                    _logger.LogDebug("Cached Seat with ID: {SeatId}", id.Value);
                }
                else
                {
                    _logger.LogDebug("Cache hit for Seat ID: {SeatId}", id.Value);
                }

                await LoadViewBagsAsync();
                _logger.LogInformation("Successfully retrieved Seat with ID: {SeatId} for editing", id.Value);
                return View(seat);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while retrieving Seat with ID: {SeatId} for edit", id.Value);
                TempData["error"] = "An error occurred while retrieving the Seat.";
                return RedirectToAction(nameof(Index));
            }
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, SeatViewModel seatVM)
        {
            if (id != seatVM.Id)
            {
                _logger.LogWarning("ID mismatch in Edit action. Route ID: {RouteId}, Model ID: {ModelId}", id, seatVM.Id);
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Edit Seat failed validation for model: {@SeatViewModel}",
                    new { seatVM.Id, seatVM.AirplaneId, seatVM.Row, seatVM.Number });
                await LoadViewBagsAsync();
                return View(seatVM);
            }

            try
            {
                _logger.LogInformation("Updating Seat with ID: {SeatId}", id);

                var existingSeat = await _unitOfWork.Repository<Seat>().GetByIdAsync(id);
                if (existingSeat == null)
                    return NotFound();
                existingSeat.AirplaneId = seatVM.AirplaneId;
                existingSeat.Row = seatVM.Row;
                existingSeat.Number = seatVM.Number;
                existingSeat.Class = seatVM.Class;
                existingSeat.Price = seatVM.Price;
                _unitOfWork.Repository<Seat>().Update(existingSeat);
                await _unitOfWork.CompleteAsync();
               
                await InvalidateSeatCache(id);
                _logger.LogInformation("Successfully updated Seat with ID: {SeatId}. Cache invalidated.", id);
                TempData["success"] = "Seat Updated Successfully";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while updating Seat with ID: {SeatId}", id);
                if (_env.IsDevelopment())
                {
                    ModelState.AddModelError(string.Empty, ex.Message);
                    _logger.LogDebug("Development error details: {ErrorMessage}", ex.Message);
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "An Error Has Occurred during Updating the Seat");
                }
                await LoadViewBagsAsync();
            }

            return View(seatVM);
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
                var seat = await _unitOfWork.Repository<Seat>().GetByIdAsync(id.Value);
                if (seat == null)
                {
                    _logger.LogWarning("Seat with ID: {SeatId} not found", id.Value);
                    return Json(new { success = false, message = "Seat not found" });
                }

                _unitOfWork.Repository<Seat>().Delete(seat);
                await _unitOfWork.CompleteAsync();
                await InvalidateSeatCache(id.Value);
                _logger.LogInformation("Seat with ID: {SeatId} deleted successfully", id.Value);
                return Json(new { success = true, message = "Delete Successful" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while retrieving Seat with ID: {SeatId} for deletion", id.Value);
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
                var cacheKey = GetSeatDetailsCacheKey(id.Value);
                var seat = await _cacheService.GetAsync<SeatViewModel>(cacheKey);

                if (seat == null)
                {
                    _logger.LogInformation("Cache miss for Seat Details ID: {SeatId}. Retrieving from database.", id.Value);
                    var spec = new BaseSpecification<Seat>(s => s.Id == id);
                    spec.Includes.Add(s => s.Airplane);
                    var seatEntity = await _unitOfWork.Repository<Seat>().GetEntityWithSpecAsync(spec);

                    if (seatEntity == null)
                    {
                        _logger.LogWarning("Seat with ID: {SeatId} not found in database", id.Value);
                        return NotFound();
                    }

                    seat = (SeatViewModel)seatEntity;

                    await _cacheService.SetAsync(cacheKey, seat, TimeSpan.FromMinutes(40));
                    await _cacheService.SetAsync(GetSeatCacheKey(id.Value), seat, TimeSpan.FromMinutes(20));

                    _logger.LogDebug("Cached Seat details with ID: {SeatId}", id.Value);
                }
                else
                {
                    _logger.LogDebug("Cache hit for Seat Details ID: {SeatId}", id.Value);
                }

                _logger.LogInformation("Successfully retrieved Seat details with ID: {SeatId}", id.Value);
                return View(seat);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while retrieving Seat details with ID: {SeatId}", id.Value);
                TempData["error"] = "An error occurred while retrieving the Seat details.";
                return RedirectToAction(nameof(Index));
            }
        }

        #endregion

        #region Helper methods

        private string GetSeatCacheKey(int id) => $"{CACHE_KEY_SEAT_PREFIX}{id}";
        private string GetSeatDetailsCacheKey(int id) => $"{CACHE_KEY_SEAT_DETAILS_PREFIX}{id}";

        private async Task InvalidateSeatCache(int? seatId = null)
        {
            try
            {
                await _cacheService.RemoveAsync(CACHE_KEY_ALL_SEATS);
                _logger.LogDebug("Removed cache for key: {CacheKey}", CACHE_KEY_ALL_SEATS);

                if (seatId.HasValue)
                {
                    var tasks = new[]
                    {
                        _cacheService.RemoveAsync(GetSeatCacheKey(seatId.Value)),
                        _cacheService.RemoveAsync(GetSeatDetailsCacheKey(seatId.Value))
                    };
                    await Task.WhenAll(tasks);

                    _logger.LogDebug("Removed specific seat cache for ID: {SeatId}", seatId.Value);
                }
                else
                {
                    await _cacheService.RemoveByPatternAsync($"{CACHE_KEY_SEAT_PREFIX}*");
                    await _cacheService.RemoveByPatternAsync($"{CACHE_KEY_SEAT_DETAILS_PREFIX}*");

                    _logger.LogDebug("Removed all seat-related caches");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating Seat cache");
            }
        }

        private async Task LoadViewBagsAsync()
        {
            ViewBag.Airplanes = (await _unitOfWork.Repository<Airplane>().GetAllAsync())
                .Select(u => new SelectListItem
                {
                    Text = u.Model,
                    Value = u.Id.ToString(),
                });

            ViewBag.SeatClasses = Enum.GetValues(typeof(SeatClass))
                .Cast<SeatClass>()
                .Select(sc => new SelectListItem
                {
                    Text = sc.ToString(),
                    Value = ((int)sc).ToString()
                });
        }

        #endregion

    }
}
