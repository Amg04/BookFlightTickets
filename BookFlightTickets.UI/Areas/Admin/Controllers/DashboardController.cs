using BookFlightTickets.Core.ServiceContracts;
using BookFlightTickets.Core.Shared.Utility;
using BookFlightTickets.Core.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace BookFlightTickets.UI.Areas.Admin.Controllers
{
    [Area(SD.Admin)]
    [Authorize(Roles = SD.Admin)]
    public class DashboardController : Controller
    {
        private readonly IDashboardService _dashboardService;
        private readonly ILogger<DashboardController> _logger;

        public DashboardController(
            IDashboardService dashboardService,
            ILogger<DashboardController> logger)
        {
            _dashboardService = dashboardService;
            _logger = logger;
        }

        #region Dashboard

        public async Task<IActionResult> Dashboard()
        {
            try
            {
                var dashboardModel = await _dashboardService.GetDashboardDataAsync();

                if (!dashboardModel.RecentFlights.Any() && !dashboardModel.RecentBookings.Any() && dashboardModel.TotalFlights == 0)
                {
                    ViewBag.WarningMessage = "No data available for dashboard.";
                    _logger.LogInformation("No dashboard data found in database");
                }
                else
                {
                    _logger.LogInformation("Dashboard loaded successfully with {RecentFlightsCount} recent flights and {RecentBookingsCount} recent bookings",
                        dashboardModel.RecentFlights.Count(), dashboardModel.RecentBookings.Count());
                }

                return View(dashboardModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while loading dashboard");
                ViewBag.ErrorMessage = "An error occurred while loading the dashboard. Please try again.";
                return View(new DashboardViewModel());
            }
        }

        #endregion

        #region GetDashboardData

        [HttpGet]
        public async Task<IActionResult> GetDashboardData()
        {
            try
            {
                var dashboardModel = await _dashboardService.GetDashboardDataAsync();
                return Json(dashboardModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting dashboard data for AJAX");
                return Json(new { error = "Failed to load data" });
            }
        }

        #endregion

    }
}
