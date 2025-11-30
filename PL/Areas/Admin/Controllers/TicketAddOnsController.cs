using BLLProject.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Utility;

namespace PL.Areas.Admin.Controllers
{
    [Area(SD.Admin)]
    [Authorize(Roles = SD.Admin)]
    public class TicketAddOnsController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IWebHostEnvironment _env;

        public TicketAddOnsController(IUnitOfWork unitOfWork, IWebHostEnvironment env)
        {
            _unitOfWork = unitOfWork;
            _env = env;
        }
        public IActionResult Index()
        {
            return View();
        }
    }
}
