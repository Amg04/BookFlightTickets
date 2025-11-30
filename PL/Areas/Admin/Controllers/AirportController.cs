using BLLProject.Interfaces;
using BLLProject.Specifications;
using DAL.models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PL.ViewModels;
using Utility;

namespace PL.Areas.Admin.Controllers
{
    [Area(SD.Admin)]
    [Authorize(Roles = SD.Admin)]
    public class AirportController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IWebHostEnvironment _env;

        public AirportController(IUnitOfWork unitOfWork , IWebHostEnvironment env)
        {
            _unitOfWork = unitOfWork;
            _env = env;
        }

        #region Index

        public async Task<IActionResult> Index()
        {
            var spec = new BaseSpecification<Airport>();
            var airports = await _unitOfWork.Repository<Airport>().GetAllWithSpecAsync(spec);

            return View(airports.Select(a => (AirportViewModel)a));
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
            if (ModelState.IsValid)
            {
                await _unitOfWork.Repository<Airport>().AddAsync((Airport)airportVM);
                int count = await _unitOfWork.CompleteAsync();
                if (count > 0)
                {
                    TempData["success"] = "Airport has been Added Successfully";
                    return RedirectToAction(nameof(Index));
                }
            }
            return View(airportVM);
        }

        #endregion

        #region Edit

        public async Task<IActionResult> Edit(int? id)
        {
            if (!id.HasValue)
                return BadRequest();

            var airport = await _unitOfWork.Repository<Airport>().GetByIdAsync(id.Value);

            if (airport == null)
                return NotFound();

            return View((AirportViewModel)airport);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, AirportViewModel airportVM)
        {
            if (id != airportVM.Id)
                return BadRequest();

            if (ModelState.IsValid)
            {
                try
                {
                    _unitOfWork.Repository<Airport>().Update((Airport)airportVM);
                    await _unitOfWork.CompleteAsync();
                    TempData["success"] = "Airport Updated Successfully";
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
                        ModelState.AddModelError(string.Empty, "An Error Has Occurred during Updating the Airport");
                    }
                }
               
            }

            return View(airportVM);
        }

        #endregion

        #region Delete & DeleteConfirmed

        public async Task<IActionResult> Delete(int? id)
        {
            if (!id.HasValue)
                return BadRequest();

            var spec = new BaseSpecification<Airport>(a => a.Id == id);

            var airport = await _unitOfWork.Repository<Airport>()
                .GetEntityWithSpecAsync(spec);

            if (airport == null)
                return NotFound();

            return View((AirportViewModel)airport);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var airport = await _unitOfWork.Repository<Airport>().GetByIdAsync(id);

            if(airport == null)
                return NotFound();

           
            _unitOfWork.Repository<Airport>().Delete(airport);
            int count = await _unitOfWork.CompleteAsync();
            if (count > 0)
                TempData["success"] = "Airport Deleted Successfully";


            return RedirectToAction(nameof(Index));
        }

        #endregion
    }
}
