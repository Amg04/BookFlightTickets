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
    public class AddOnController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IWebHostEnvironment _env;

        public AddOnController(IUnitOfWork unitOfWork, IWebHostEnvironment env)
        {
            _unitOfWork = unitOfWork;
            _env = env;
        }

        #region Index

        public async Task<IActionResult> Index()
        {
            var addons = await _unitOfWork.Repository<AddOn>().GetAllAsync();
            return View(addons.Select(a => (AddOnViewModel)a));
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
            if (ModelState.IsValid)
            {
                await _unitOfWork.Repository<AddOn>().AddAsync((AddOn)addonVM);
                int count = await _unitOfWork.CompleteAsync();
                if (count > 0)
                {
                    TempData["success"] = "AddOn has been Added Successfully";
                    return RedirectToAction(nameof(Index));
                }
            }
            return View(addonVM);
        }

        #endregion

        #region Edit

        public async Task<IActionResult> Edit(int? id)
        {
            if (!id.HasValue)
                return BadRequest();

            var addon = await _unitOfWork.Repository<AddOn>().GetByIdAsync(id.Value);
            if (addon == null)
                return NotFound();

            return View((AddOnViewModel)addon);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, AddOnViewModel addon)
        {
            if (id != addon.Id)
                return NotFound();

            try
            {
                if (ModelState.IsValid)
                {
                    _unitOfWork.Repository<AddOn>().Update((AddOn)addon);
                    await _unitOfWork.CompleteAsync();
                    TempData["success"] = "AddOn Updated Successfully";
                    return RedirectToAction(nameof(Index));
                }
            }
            catch (Exception ex)
            {
                if (_env.IsDevelopment())
                {
                    ModelState.AddModelError(string.Empty, ex.Message);
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

        public async Task<IActionResult> Delete(int? id)
        {
            if (!id.HasValue)
                return BadRequest();

            var addon = await _unitOfWork.Repository<AddOn>().GetByIdAsync(id.Value);

            if (addon == null)
                return NotFound();

            return View((AddOnViewModel)addon);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var addon = await _unitOfWork.Repository<AddOn>().GetByIdAsync(id);
            if (addon != null)
            {
                _unitOfWork.Repository<AddOn>().Delete(addon);
                int count =await _unitOfWork.CompleteAsync();
                if (count > 0)
                    TempData["success"] = "AddOn Deleted Successfully";
            }
            return RedirectToAction(nameof(Index));
        }

        #endregion
    }
}
