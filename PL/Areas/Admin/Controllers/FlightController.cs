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
    public class FlightController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IWebHostEnvironment _env;

        public FlightController(IUnitOfWork unitOfWork, IWebHostEnvironment env)
        {
            _unitOfWork = unitOfWork;
            _env = env;
        }

        #region Index

        public async Task<IActionResult> Index()
        {
            var spec = new BaseSpecification<Flight>();
            spec.Includes.Add(f => f.Airline);
            spec.Includes.Add(f => f.Airplane);
            spec.Includes.Add(f => f.DepartureAirport);
            spec.Includes.Add(f => f.ArrivalAirport);

            var flights = await _unitOfWork.Repository<Flight>().GetAllWithSpecAsync(spec);
            return View(flights.Select(f => (FlightViewModel)f));
        }

        #endregion

        #region Create

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            await PopulateDropDownLists();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(FlightViewModel flightVM)
        {
            if (ModelState.IsValid)
            {
                var flight = (Flight)flightVM;
                await _unitOfWork.Repository<Flight>().AddAsync(flight);
                int count = await _unitOfWork.CompleteAsync();
                if (count > 0)
                {
                    var spec = new BaseSpecification<Airplane>();
                    spec.Includes.Add(a => a.SeatTemplates);
                    var airplane = await _unitOfWork.Repository<Airplane>().GetEntityWithSpecAsync(spec);

                    foreach (var seatTemplate in airplane!.SeatTemplates)
                    {
                        var flightSeat = new FlightSeat
                        {
                            FlightId = flight.Id,
                            SeatId = seatTemplate.Id,
                            IsAvailable = true
                        };
                        await _unitOfWork.Repository<FlightSeat>().AddAsync(flightSeat);
                    }
                    await _unitOfWork.CompleteAsync();
                }

                TempData["success"] = "Flight has been Added Successfully";
                return RedirectToAction(nameof(Index));
            }

            await PopulateDropDownLists(airlineId: flightVM.AirlineId);
            return View(flightVM);
        }

        #endregion

        #region Edit


        [HttpGet]
        public async Task<IActionResult> Edit(int? id)
        {
            if (!id.HasValue)
                return BadRequest();

            var flight = await _unitOfWork.Repository<Flight>().GetByIdAsync(id.Value);

            if (flight == null)
                return NotFound();

            await PopulateDropDownLists(airlineId: flight.AirlineId);
            return View((FlightViewModel)flight);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, FlightViewModel flightVM)
        {
            if (id != flightVM.Id)
                return BadRequest();

            if (ModelState.IsValid)
            {
                try
                {

                    _unitOfWork.Repository<Flight>().Update((Flight)flightVM);
                    await _unitOfWork.CompleteAsync();
                    TempData["success"] = "Flight Updated Successfully";
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
                        ModelState.AddModelError(string.Empty, "An Error Has Occurred during Updating the flight");
                    }
                }
            }
            await PopulateDropDownLists(airlineId: flightVM.AirlineId);
            return View(flightVM);
        }

        #endregion

        #region Delete

        [HttpGet]
        public async Task<IActionResult> Delete(int? id, string viewname = "Delete")
        {
            if (!id.HasValue)
                return BadRequest();

            var spec = new BaseSpecification<Flight>(f => f.Id == id);
            spec.Includes.Add(f => f.Airline);
            spec.Includes.Add(f => f.Airplane);
            spec.Includes.Add(f => f.ArrivalAirport);
            spec.Includes.Add(f => f.DepartureAirport);
            var flight = await _unitOfWork.Repository<Flight>()
                .GetEntityWithSpecAsync(spec);

            if (flight == null)
                return NotFound();

            return View(viewname, (FlightViewModel)flight);
        }

        [HttpPost]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {

            var flight = await _unitOfWork.Repository<Flight>().GetByIdAsync(id);

            if (flight == null)
                return NotFound();

            _unitOfWork.Repository<Flight>().Delete(flight);
            int count = await _unitOfWork.CompleteAsync();
            if (count > 0)
                TempData["success"] = "flight Deleted Successfully";

            return RedirectToAction(nameof(Index));
        }

        #endregion

        #region Details

        public async Task<IActionResult> Details(int? id)
        {
            return await Delete(id, nameof(Details));
        }

        #endregion

        #region Search

        public async Task<IActionResult> Search(string? keyword, DateTime? date)
        {
            var spec = new BaseSpecification<Flight>();
            spec.Includes.Add(f => f.Airline);
            spec.Includes.Add(f => f.Airplane);
            spec.Includes.Add(f => f.DepartureAirport);
            spec.Includes.Add(f => f.ArrivalAirport);

            if (!string.IsNullOrEmpty(keyword))
            {
                spec.Criteria = f => (f.Airline.Name.Contains(keyword) ||
                    f.DepartureAirport.Name.Contains(keyword) ||
                    f.ArrivalAirport.Name.Contains(keyword));
            }
            if (date.HasValue)
            {
                spec.Criteria = f => (f.DepartureTime.Date == date.Value.Date);
            }

            var flights = await _unitOfWork.Repository<Flight>().GetAllWithSpecAsync(spec);
            ViewBag.keyword = keyword;
            ViewBag.date = date;
            return View(flights.Select(f => (FlightViewModel)f));
        }

        #endregion

        #region GetAirplanesByAirlineId

        [HttpGet]
        public async Task<IActionResult> GetAirplanesByAirlineId(int airlineId)
        {
            var spec = new BaseSpecification<Airplane>(e => e.AirlineId == airlineId);
            var airplanes = (await _unitOfWork.Repository<Airplane>().GetAllWithSpecAsync(spec))
                .Select(e => new { Id = e.Id, Model = e.Model });

            return new JsonResult(airplanes);
        }

        #endregion

        #region method
        private async Task PopulateDropDownLists(int? airlineId = null)
        {
            ViewBag.Airlines = (await _unitOfWork.Repository<Airline>().GetAllAsync())
                    .Select(u => new SelectListItem
                    {
                        Text = u.Name,
                        Value = u.Id.ToString(),
                    });

            if (airlineId.HasValue)
            {
                var airplanes = await _unitOfWork.Repository<Airplane>()
                    .GetAllWithSpecAsync(new BaseSpecification<Airplane>(m => m.AirlineId == airlineId.Value));

                ViewBag.Airplanes = airplanes.Select(u => new SelectListItem
                {
                    Text = u.Model,
                    Value = u.Id.ToString()
                });
            }
            else
            {
                ViewBag.Airplanes = new SelectList(Enumerable.Empty<SelectListItem>());
            }

            ViewBag.Airports = (await _unitOfWork.Repository<Airport>().GetAllAsync())
                   .Select(u => new SelectListItem
                   {
                       Text = u.Name,
                       Value = u.Id.ToString(),
                   });
        }

        #endregion
    }
}
