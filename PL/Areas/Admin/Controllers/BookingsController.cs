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
    public class BookingsController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IWebHostEnvironment _env;

        public BookingsController(IUnitOfWork unitOfWork, IWebHostEnvironment env)
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
    }
}
