using BAL.model;
using BLLProject.Interfaces;
using BLLProject.Specifications;
using DAL.models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Utility;

namespace PL.Areas.Admin.Controllers
{
    [Area(SD.Admin)]
    [Authorize(Roles = SD.Admin)]
    public class DashboardController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserManager<AppUser> _userManager;
        public DashboardController(IUnitOfWork unitOfWork,UserManager<AppUser> userManager)
        {
            _unitOfWork = unitOfWork;
            _userManager = userManager;
        }

        #region Dashboard

        public async Task<IActionResult> Dashboard()
        {
            var flightsspec = new BaseSpecification<Flight>();
            flightsspec.Includes.Add(f => f.Airline);
            flightsspec.Includes.Add(f => f.Airplane);
            flightsspec.Includes.Add(f => f.DepartureAirport);
            flightsspec.Includes.Add(f => f.ArrivalAirport);
            flightsspec.OrderByDesc(f => f.DepartureTime);
            flightsspec.ApplyPaging(0, 5); 
            
            var bookingsspec = new BaseSpecification<Booking>();
            bookingsspec.Includes.Add(f => f.Flight);
            bookingsspec.OrderByDesc(f => f.BookingDate);
            bookingsspec.ApplyPaging(0, 5); 

            var model = new DashboardViewModel
            {
                TotalUsers = await _userManager.Users.CountAsync(),
                TotalFlights = await _unitOfWork.Repository<Flight>().CountAsync(),
                TotalAirlines = await _unitOfWork.Repository<Airline>().CountAsync(),
                TotalAirplanes = await _unitOfWork.Repository<Airplane>().CountAsync(),
                TotalBookings = await _unitOfWork.Repository<Booking>().CountAsync(),
                FlightsByAirline = await _unitOfWork.FlightRepository.GetFlightCountByAirlineAsync(),
                RecentFlights = await _unitOfWork.Repository<Flight>().GetAllWithSpecAsync(flightsspec),
                RecentBookings = await _unitOfWork.Repository<Booking>().GetAllWithSpecAsync(bookingsspec)
            };

            // Generate months
            var months = Enumerable.Range(0, 6)
                .Select(i => DateTime.Now.AddMonths(-i))
                .OrderBy(m => m)
                .ToList();

            model.MonthlyLabels = months.Select(m => m.ToString("MMM yyyy")).ToList();

            // Create specifications for each month
            var countTasks = months.Select(async m =>
            {
                var spec = new BaseSpecification<Flight>(
                    f => f.DepartureTime.Month == m.Month && f.DepartureTime.Year == m.Year
                );
                return await _unitOfWork.FlightRepository.CountAsync(spec);
            });

            // Execute all tasks in parallel
            model.MonthlyFlights = (await Task.WhenAll(countTasks)).ToList();

            return View(model);
        }

        #endregion

    }
}
