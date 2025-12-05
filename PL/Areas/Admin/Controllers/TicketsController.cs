using BLLProject.Interfaces;
using BLLProject.Specifications;
using DAL.models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PL.ViewModels;
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
            var spec = new BaseSpecification<Ticket>();
            spec.ComplexIncludes.Add(c => c.Include(t => t.FlightSeat)
                .ThenInclude(a => a.Seat));
            var tickets = await _unitOfWork.Repository<Ticket>().GetAllWithSpecAsync(spec);
            return View(tickets.Select(a => (TicketViewModel)a));
        }

        #endregion
    }
}
