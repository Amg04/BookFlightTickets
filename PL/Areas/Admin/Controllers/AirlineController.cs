using BLLProject.Interfaces;
using DAL.models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PL.ViewModels;
using Utility;

namespace PL.Areas.Admin.Controllers
{
    [Area(SD.Admin)]
    [Authorize(Roles = SD.Admin)]
    public class AirlineController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IWebHostEnvironment _env;

        public AirlineController(IUnitOfWork unitOfWork, IWebHostEnvironment env)
        {
            _unitOfWork = unitOfWork;
            _env = env;
        }

        #region Index

        public async Task<IActionResult> Index()
        {
            var airlines = await _unitOfWork.Repository<Airline>().GetAllAsync();
            return View(airlines.Select(a => (AirlineViewModel)a));
        }

        #endregion

        #region Create

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(AirlineViewModel airline)
        {
            if (ModelState.IsValid)
            {
                await _unitOfWork.Repository<Airline>().AddAsync((Airline)airline);
                int count = await _unitOfWork.CompleteAsync();
                if (count > 0)
                {
                    TempData["success"] = "Airline has been Added Successfully";
                    return RedirectToAction(nameof(Index));
                }
            }
            return View(airline);
        }

        #endregion

        #region Edit

        public async Task<IActionResult> Edit(int? id)
        {
            if (!id.HasValue)
                return BadRequest();

            var airline = await _unitOfWork.Repository<Airline>().GetByIdAsync(id.Value);
            if (airline == null)
                return NotFound();
            return View((AirlineViewModel)airline);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, AirlineViewModel airline)
        {
            if (id != airline.Id)
                return BadRequest();

            if (ModelState.IsValid)
            {
                try
                {
                    _unitOfWork.Repository<Airline>().Update((Airline)airline);
                    await _unitOfWork.CompleteAsync();
                    TempData["success"] = "Airline Updated Successfully";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    if (_env.IsDevelopment())
                    {
                        ModelState.AddModelError(string.Empty, ex.Message);
                    }
                    else
                    {
                        ModelState.AddModelError(string.Empty, "An Error Has Occurred during Updating the Airline");
                    }
                }

            }
            return View(airline);
        }

        #endregion

        #region Delete & DeleteConfirmed

        public async Task<IActionResult> Delete(int? id)
        {
            if (!id.HasValue)
                return BadRequest();

            var airline = await _unitOfWork.Repository<Airline>().GetByIdAsync(id.Value);
            if (airline == null)
                return NotFound();

            return View((AirlineViewModel)airline);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var airline = await _unitOfWork.Repository<Airline>().GetByIdAsync(id);

            if (airline == null)
                return NotFound();

            _unitOfWork.Repository<Airline>().Delete(airline);
            int count = await _unitOfWork.CompleteAsync();
            if (count > 0)
                TempData["success"] = "Airline Deleted Successfully";

            return RedirectToAction(nameof(Index));
        }

        #endregion

        #region Details

        public async Task<IActionResult> Details(int? id)
        {
            if (!id.HasValue)
                return BadRequest();

            var airline = await _unitOfWork.Repository<Airline>().GetByIdAsync(id.Value);

            if (airline == null)
                return NotFound();

            return View((AirlineViewModel)airline);
        }

        #endregion

    }
}
