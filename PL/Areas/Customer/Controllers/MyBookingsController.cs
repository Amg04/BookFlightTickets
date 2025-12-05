using BLLProject.Interfaces;
using BLLProject.Specifications;
using DAL.models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rotativa.AspNetCore;
using System.Security.Claims;
using Utility;

namespace PL.Areas.Customer.Controllers
{
    [Area(SD.Customer)]
    [Authorize]
    public class MyBookingsController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        public MyBookingsController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        #region Index
        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var spec = new BaseSpecification<Booking>(b => b.UserId == userId);
            spec.ComplexIncludes.Add(c => c.Include(t => t.Tickets)
                  .ThenInclude(a => a.TicketAddOns)
                  .ThenInclude(a => a.AddOn));
            spec.ComplexIncludes.Add(c => c.Include(t => t.Flight)
                .ThenInclude(a => a.FlightSeats)
                .ThenInclude(a => a.Seat));
            spec.ComplexIncludes.Add(c => c.Include(t => t.Flight)
                .ThenInclude(a => a.DepartureAirport));
            spec.ComplexIncludes.Add(c => c.Include(t => t.Flight)
                .ThenInclude(a => a.ArrivalAirport));
            spec.ComplexIncludes.Add(c => c.Include(t => t.Flight)
                .ThenInclude(a => a.Airline));
            spec.ComplexIncludes.Add(c => c.Include(t => t.Flight)
                .ThenInclude(a => a.Airplane));
            spec.Includes.Add(b => b.Payment!);
            var MyBookings = await _unitOfWork.Repository<Booking>().GetAllWithSpecAsync(spec);
            return View(MyBookings);
        }

        #endregion

        #region BookingPDF

        //Rotativa.AspNetCore
        public async Task<IActionResult> BookingPDF(int bookingId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var spec = new BaseSpecification<Booking>(b => b.Id == bookingId && b.UserId == userId);
            spec.ComplexIncludes.Add(c => c.Include(t => t.Tickets)
                 .ThenInclude(a => a.TicketAddOns)
                 .ThenInclude(a => a.AddOn));
            spec.ComplexIncludes.Add(c => c.Include(t => t.Flight)
                .ThenInclude(a => a.FlightSeats)
                .ThenInclude(a => a.Seat));
            spec.ComplexIncludes.Add(c => c.Include(t => t.Flight)
                .ThenInclude(a => a.DepartureAirport));
            spec.ComplexIncludes.Add(c => c.Include(t => t.Flight)
                .ThenInclude(a => a.ArrivalAirport));
            spec.ComplexIncludes.Add(c => c.Include(t => t.Flight)
                .ThenInclude(a => a.Airline));
            spec.ComplexIncludes.Add(c => c.Include(t => t.Flight)
                .ThenInclude(a => a.Airplane));
            spec.Includes.Add(b => b.Payment!);
            var booking = await _unitOfWork.Repository<Booking>().GetEntityWithSpecAsync(spec);
            if (booking == null)
                return NotFound();

            return new ViewAsPdf("BookingPDF", booking, ViewData)
            {
                PageMargins = new Rotativa.AspNetCore.Options.Margins() { Top = 20, Right = 20, Bottom = 20, Left = 20 },
                PageOrientation = Rotativa.AspNetCore.Options.Orientation.Portrait,
                FileName = $"Booking_{booking.PNR}.pdf",
                PageSize = Rotativa.AspNetCore.Options.Size.A4
            };
        }



        #endregion
    }
}
