using BookFlightTickets.Core.Domain.Entities;
using BookFlightTickets.Core.Domain.RepositoryContracts;
using BookFlightTickets.Core.Domain.Specifications;
using BookFlightTickets.Core.ServiceContracts;
using BookFlightTickets.Core.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
namespace BookFlightTickets.Core.Services
{
    public class DashboardService : IDashboardService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserManager<AppUser> _userManager;
        private readonly ILogger<DashboardService> _logger;

        public DashboardService(
            IUnitOfWork unitOfWork,
            UserManager<AppUser> userManager,
            ILogger<DashboardService> logger)
        {
            _unitOfWork = unitOfWork;
            _userManager = userManager;
            _logger = logger;
        }

        public async Task<DashboardViewModel> GetDashboardDataAsync()
        {
            try
            {
                _logger.LogInformation("Loading dashboard data from database");

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

                var monthlyData = await GetMonthlyFlightsData();

                var dashboardModel = new DashboardViewModel
                {
                    TotalUsers = await _userManager.Users.CountAsync(),
                    TotalFlights = await _unitOfWork.Repository<Flight>().CountAsync(),
                    TotalAirlines = await _unitOfWork.Repository<Airline>().CountAsync(),
                    TotalAirplanes = await _unitOfWork.Repository<Airplane>().CountAsync(),
                    TotalBookings = await _unitOfWork.Repository<Booking>().CountAsync(),
                    FlightsByAirline = await SafeGetFlightsByAirline() ?? new Dictionary<string, int>(),
                    RecentFlights = (await _unitOfWork.Repository<Flight>().GetAllWithSpecAsync(flightsspec))
                        .Select(f => (FlightViewModel)f).ToList(),
                    RecentBookings = (await _unitOfWork.Repository<Booking>().GetAllWithSpecAsync(bookingsspec))
                        .Select(b => (BookingViewModel)b).ToList(),
                    MonthlyLabels = monthlyData.MonthlyLabels,
                    MonthlyFlights = monthlyData.MonthlyFlights
                };

                return dashboardModel;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while loading dashboard data");
                throw;
            }
        }

        private async Task<Dictionary<string, int>> SafeGetFlightsByAirline()
        {
            try
            {
                return await _unitOfWork.FlightRepository.GetFlightCountByAirlineAsync()
                       ?? new Dictionary<string, int>();
            }
            catch
            {
                return new Dictionary<string, int>();
            }
        }

        private async Task<(List<string> MonthlyLabels, List<int> MonthlyFlights)> GetMonthlyFlightsData()
        {
            try
            {
                var months = Enumerable.Range(0, 6)
                     .Select(i => DateTime.Now.AddMonths(-i))
                     .OrderBy(m => m)
                     .ToList();

                var monthlyLabels = months.Select(m => m.ToString("MMM yyyy")).ToList();
                var monthlyFlights = new List<int>();

                foreach (var month in months)
                {
                    var spec = new BaseSpecification<Flight>(f => f.DepartureTime.Month == month.Month && f.DepartureTime.Year == month.Year);
                    var count = await _unitOfWork.FlightRepository.CountAsync(spec);
                    monthlyFlights.Add(count);
                }

                return (monthlyLabels, monthlyFlights);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting monthly flights data");
                return (new List<string>(), new List<int>());
            }
        }
    }
}
