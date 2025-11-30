using BLLProject.Interfaces;
using BLLProject.Specifications;
using DAL.models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using PL.ViewModels;
using Utility;

namespace PL.Areas.Admin.Controllers
{
    [Area(SD.Admin)]
    [Authorize(Roles = SD.Admin)]
    public class AirplaneController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IWebHostEnvironment _env;

        public AirplaneController(IUnitOfWork unitOfWork, IWebHostEnvironment env)
        {
            _unitOfWork = unitOfWork;
            _env = env;
        }

        #region Index

        public async Task<IActionResult> Index()
        {
            var spec = new BaseSpecification<Airplane>();
            spec.Includes.Add(f => f.Airline);
            var airplanes = await _unitOfWork.Repository<Airplane>().GetAllWithSpecAsync(spec);

            return View(airplanes.Select(a => (AirplaneViewModel)a));
        }

        #endregion

        #region Create

        public async Task<IActionResult> Create()
        {
            ViewBag.Airlines = (await _unitOfWork.Repository<Airline>().GetAllAsync())
                  .Select(u => new SelectListItem
                  {
                      Text = u.Name,
                      Value = u.Id.ToString(),
                  });

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(AirplaneViewModel airplane)
        {
            if (ModelState.IsValid)
            {
                await _unitOfWork.Repository<Airplane>().AddAsync((Airplane)airplane);
                int count = await _unitOfWork.CompleteAsync();
                if (count > 0)
                {
                    TempData["success"] = "Airplane has been Added Successfully";
                    return RedirectToAction(nameof(Index));
                }
            }

            ViewBag.Airlines = (await _unitOfWork.Repository<Airline>().GetAllAsync())
                  .Select(u => new SelectListItem
                  {
                      Text = u.Name,
                      Value = u.Id.ToString(),
                  });
            return View(airplane);
        }

        #endregion

        #region Edit

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return BadRequest();

            var airplane = await _unitOfWork.Repository<Airplane>().GetByIdAsync(id.Value);

            if (airplane == null)
                return NotFound();

            ViewBag.Airlines = (await _unitOfWork.Repository<Airline>().GetAllAsync())
                 .Select(u => new SelectListItem
                 {
                     Text = u.Name,
                     Value = u.Id.ToString(),
                 });

            return View((AirplaneViewModel)airplane);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, AirplaneViewModel airplaneVM)
        {
            if (id != airplaneVM.Id)
                return BadRequest();

            if (ModelState.IsValid)
            {
                _unitOfWork.Repository<Airplane>().Update((Airplane)airplaneVM);
                await _unitOfWork.CompleteAsync();
                TempData["success"] = "Airplane Updated Successfully";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Airlines = (await _unitOfWork.Repository<Airline>().GetAllAsync())
                .Select(u => new SelectListItem
                {
                    Text = u.Name,
                    Value = u.Id.ToString(),
                });

            return View(airplaneVM);
        }

        #endregion

        #region Delete & DeleteConfirmed

        public async Task<IActionResult> Delete(int? id)
        {
            if (!id.HasValue)
                return BadRequest();


            var airplane = await _unitOfWork.Repository<Airplane>()
                .GetByIdAsync(id.Value);

            if (airplane == null)
                return NotFound();

            return View((AirplaneViewModel)airplane);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var airplane = await _unitOfWork.Repository<Airplane>().GetByIdAsync(id);

            if(airplane == null)
                return NotFound();

                _unitOfWork.Repository<Airplane>().Delete(airplane);
                int count = await _unitOfWork.CompleteAsync();
                if (count > 0)
                    TempData["success"] = "Airplane Deleted Successfully";

            return RedirectToAction(nameof(Index));
        }

        #endregion
    }
}
