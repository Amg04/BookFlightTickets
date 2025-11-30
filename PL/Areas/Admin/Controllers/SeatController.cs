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
    public class SeatController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IWebHostEnvironment _env;

        public SeatController(IUnitOfWork unitOfWork, IWebHostEnvironment env)
        {
            _unitOfWork = unitOfWork;
            _env = env;
        }

        #region Index

        public async Task<IActionResult> Index()
        {
            var spec = new BaseSpecification<Seat>();
            spec.Includes.Add(s => s.Airplane);
            var seats = await _unitOfWork.Repository<Seat>().GetAllWithSpecAsync(spec);
            return View(seats.Select(a => (SeatViewModel)a));
        }

        #endregion

        #region Create

        public async Task<IActionResult> Create()
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

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SeatViewModel seatVM)
        {
            if (ModelState.IsValid)
            {
                
                var checkAvailableSeat = await _unitOfWork.Repository<Seat>()
                    .CountAsync(new BaseSpecification<Seat>(a => a.AirplaneId == seatVM.AirplaneId));

                var airplane = await _unitOfWork.Repository<Airplane>().GetByIdAsync(seatVM.AirplaneId);

                if (checkAvailableSeat < airplane.SeatCapacity)
                {
                    await _unitOfWork.Repository<Seat>().AddAsync((Seat)seatVM);
                    int count = await _unitOfWork.CompleteAsync();
                    if (count > 0)
                    {
                        TempData["success"] = "Seat has been Added Successfully";
                        return RedirectToAction(nameof(Index));
                    }
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "The seat capacity is full. You cannot add more seats.");
                }
                
            }
            return View(seatVM);
        }

        #endregion

        #region Edit

        public async Task<IActionResult> Edit(int? id)
        {
            if (!id.HasValue)
                return BadRequest();

            var seat = await _unitOfWork.Repository<Seat>().GetByIdAsync(id.Value);
            if (seat == null)
                return NotFound();

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

            return View((SeatViewModel)seat);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, SeatViewModel seatVM)
        {
            if (id != seatVM.Id)
                return BadRequest();

            if (ModelState.IsValid)
            {
                try
                {
                    _unitOfWork.Repository<Seat>().Update((Seat)seatVM);
                    await _unitOfWork.CompleteAsync();
                    TempData["success"] = "Seat Updated Successfully";
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
                        ModelState.AddModelError(string.Empty, "An Error Has Occurred during Updating the Seat");
                    }
                }

            }
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

            return View(seatVM);
        }

        #endregion

        #region Delete & DeleteConfirmed

        public async Task<IActionResult> Delete(int? id)
        {
            if (!id.HasValue)
                return BadRequest();

            var spec = new BaseSpecification<Seat>(s => s.Id == id);
            spec.Includes.Add(s => s.Airplane);
            var seat = await _unitOfWork.Repository<Seat>().GetEntityWithSpecAsync(spec);

            if (seat == null)
                return NotFound();

            return View((SeatViewModel)seat);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var seat = await _unitOfWork.Repository<Seat>().GetByIdAsync(id);

            if (seat == null)
                return NotFound();

            _unitOfWork.Repository<Seat>().Delete(seat);
            int count = await _unitOfWork.CompleteAsync();
            if (count > 0)
                TempData["success"] = "Seat Deleted Successfully";

            return RedirectToAction(nameof(Index));
        }

        #endregion

        #region Details

        public async Task<IActionResult> Details(int? id)
        {
            if (!id.HasValue)
                return BadRequest();

            var spec = new BaseSpecification<Seat>(s => s.Id == id);
            spec.Includes.Add(s => s.Airplane);
            var seat = await _unitOfWork.Repository<Seat>().GetEntityWithSpecAsync(spec);

            if (seat == null)
                return NotFound();

            return View((SeatViewModel)seat);
        }

        #endregion
    
    }
}
