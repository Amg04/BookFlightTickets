using BLLProject.Interfaces;
using BLLProject.Specifications;
using DAL.models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using PL.ViewModels;
using System.Security.Claims;
using System.Threading.Tasks;
using Utility;

namespace PL.Areas.Admin.Controllers
{
    [Area(SD.Admin)]
    [Authorize(Roles = SD.Admin)]
    public class TicketsController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IWebHostEnvironment _env;

        public TicketsController(IUnitOfWork unitOfWork, IWebHostEnvironment env)
        {
            _unitOfWork = unitOfWork;
            _env = env;
        }

        #region Index

        public async Task<IActionResult> Index()
        {
            var spec = new BaseSpecification<Booking>();
            spec.Includes.Add(b => b.Flight);
            spec.Includes.Add(b => b.Payment);
            var bookings = await _unitOfWork.Repository<Booking>().GetAllWithSpecAsync(spec);
            return View(bookings.Select(a => (BookingViewModel)a));
        }

        #endregion


        #region Create

        public async Task<IActionResult> Create()
        {
            ViewBag.Flights = (await _unitOfWork.Repository<Flight>().GetAllAsync())
                  .Select(u => new SelectListItem
                  {
                      Text = u.Id.ToString(),
                      Value = u.Id.ToString(),
                  });

            ViewBag.Status = Enum.GetValues(typeof(Status))
              .Cast<Status>()
              .Select(sc => new SelectListItem
              {
                  Text = sc.ToString(),
                  Value = ((int)sc).ToString()
              });

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(BookingViewModel bookingVM)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();
           
            if (ModelState.IsValid)
            {
                var booking = (Booking)bookingVM;
                booking.UserId = userId;
                await _unitOfWork.Repository<Booking>().AddAsync(booking);
                int count = await _unitOfWork.CompleteAsync();
                if (count > 0)
                {
                    TempData["success"] = "Booking has been Added Successfully";
                    return RedirectToAction(nameof(Index));
                }
            }
            ViewBag.Flights = (await _unitOfWork.Repository<Flight>().GetAllAsync())
                 .Select(u => new SelectListItem
                 {
                     Text = u.Id.ToString(),
                     Value = u.Id.ToString(),
                 });

            ViewBag.Status = Enum.GetValues(typeof(Status))
              .Cast<Status>()
              .Select(sc => new SelectListItem
              {
                  Text = sc.ToString(),
                  Value = ((int)sc).ToString()
              });

            return View(bookingVM);
        }

        #endregion


    }
}
