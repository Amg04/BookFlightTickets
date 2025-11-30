using BLLProject.Interfaces;
using BLLProject.Specifications;
using DAL.models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PL.ViewModels;
using Utility;
using X.PagedList;

namespace PL.Areas.Customer.Controllers
{
    [Area(SD.Customer)]
    public class FlightController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        public FlightController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        #region Index

        [HttpGet]
        public async Task<IActionResult> Index(FlightFilterViewModel filterDto)
        {
            if (!string.IsNullOrEmpty(filterDto.filterButton) && !string.IsNullOrEmpty(filterDto.filterInput))
            {
                if (int.TryParse(filterDto.filterInput, out int num))
                {
                    filterDto.filterbynum = num;
                    filterDto.filterstring = null;
                }
                else
                {
                    filterDto.filterstring = filterDto.filterInput;
                    filterDto.filterbynum = null;
                }
            }

            var spec = new BaseSpecification<Flight>(f =>
                (string.IsNullOrEmpty(filterDto.filterstring) ||
                 f.Airline.Name.ToLower().Contains(filterDto.filterstring.ToLower()) ||
                 f.ArrivalAirport.Name.ToLower().Contains(filterDto.filterstring.ToLower()) ||
                 f.DepartureAirport.Name.ToLower().Contains(filterDto.filterstring.ToLower())) &&
                (!filterDto.filterbynum.HasValue || f.BasePrice <= filterDto.filterbynum.Value) &&
                (string.IsNullOrEmpty(filterDto.From) || f.DepartureAirport.Name.ToLower().Contains(filterDto.From.ToLower())) &&
                (string.IsNullOrEmpty(filterDto.to) || f.ArrivalAirport.Name.ToLower().Contains(filterDto.to.ToLower())) &&
                 f.FlightSeats.Any(s => s.IsAvailable)
            );

            spec.Includes.Add(f => f.Airline);
            spec.Includes.Add(f => f.Airplane);
            spec.Includes.Add(f => f.FlightSeats);
            spec.Includes.Add(f => f.DepartureAirport);
            spec.Includes.Add(f => f.ArrivalAirport);
         
            if (!string.IsNullOrEmpty(filterDto.sortBy))
            {
                bool desc = filterDto.sortDir?.ToLower() == "desc";

                switch (filterDto.sortBy.ToLower())
                {
                    case "price":
                        if (desc) spec.OrderByDesc(f => f.BasePrice);
                        else spec.OrderByAsc(f => f.BasePrice);
                        break;

                    case "departure":
                        if (desc) spec.OrderByDesc(f => f.DepartureTime);
                        else spec.OrderByAsc(f => f.DepartureTime);
                        break;

                    case "airline":
                        if (desc) spec.OrderByDesc(f => f.Airline.Name);
                        else spec.OrderByAsc(f => f.Airline.Name);
                        break;

                    default:
                        spec.OrderByAsc(f => f.Id);
                        break;
                }
            }
            else
            {
                spec.OrderByAsc(f => f.Id);
            }

            var pageNumber = filterDto.page ?? 1;
            int pageSize = 5;

            var totalCount = await _unitOfWork.FlightRepository.CountAsync(spec);

            int skip = (pageNumber - 1) * pageSize;
            spec.ApplyPaging(skip, pageSize);

            var flightsData = await _unitOfWork.Repository<Flight>().GetAllWithSpecAsync(spec);
            var pagedFlights = new StaticPagedList<Flight>(flightsData, pageNumber, pageSize, totalCount);

            ViewBag.AvailableSeats = flightsData.ToDictionary(
                f => f.Id,
                f => f.FlightSeats.Count(s => s.IsAvailable));
            ViewBag.FilterModel = filterDto;
            ViewBag.pageNumber = pageNumber;

            return View(pagedFlights);
        }


        #endregion

        #region Details

        [HttpGet("{id}")]
        public async Task<IActionResult> Details(int id)
        {
            var spec = new BaseSpecification<Flight>(f => f.Id == id);
            spec.Includes.Add(f => f.Airline);
            spec.Includes.Add(f => f.Airplane);
            spec.Includes.Add(f => f.FlightSeats);
            spec.Includes.Add(f => f.DepartureAirport);
            spec.Includes.Add(f => f.ArrivalAirport);

            var flight = await _unitOfWork.Repository<Flight>().GetEntityWithSpecAsync(spec);
            if (flight == null)
                return NotFound();
            ViewBag.AvailableSeats = flight.FlightSeats.Count(s => s.IsAvailable);
            return View(flight);
        }

        #endregion
    }
}
