using BookFlightTickets.Core.ServiceContracts;
using BookFlightTickets.Core.ViewModels;
using BookFlightTickets.UI.Areas.Admin.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;

namespace BookFlightTickets.ControllerTests.Admin
{
    public class DashboardControllerTests
    {
        private readonly Mock<IDashboardService> _dashboardServiceMock;
        private readonly Mock<ILogger<DashboardController>> _loggerMock;
        private readonly DashboardController _controller;
        private readonly ITempDataDictionary _tempData;

        public DashboardControllerTests()
        {
            _dashboardServiceMock = new Mock<IDashboardService>();
            _loggerMock = new Mock<ILogger<DashboardController>>();

            _controller = new DashboardController(
                _dashboardServiceMock.Object,
                _loggerMock.Object
            );

            _tempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>());
            _controller.TempData = _tempData;
        }

        #region Dashboard Action

        [Fact]
        public async Task Dashboard_ShouldReturnViewWithModel_WhenDataExists()
        {
            // Arrange
            var dashboardModel = new DashboardViewModel
            {
                TotalUsers = 10,
                TotalFlights = 5,
                TotalAirlines = 3,
                TotalAirplanes = 4,
                TotalBookings = 8,
                FlightsByAirline = new Dictionary<string, int> { { "Airline A", 2 } },
                RecentFlights = new List<FlightViewModel> { new FlightViewModel() },
                RecentBookings = new List<BookingViewModel> { new BookingViewModel() },
                MonthlyLabels = new List<string> { "Jan", "Feb" },
                MonthlyFlights = new List<int> { 1, 2 }
            };

            _dashboardServiceMock
                .Setup(s => s.GetDashboardDataAsync())
                .ReturnsAsync(dashboardModel);

            // Act
            var result = await _controller.Dashboard();

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<DashboardViewModel>(viewResult.Model);
            Assert.Equal(10, model.TotalUsers);
            Assert.Null(_controller.ViewBag.WarningMessage);
            Assert.Null(_controller.ViewBag.ErrorMessage);
        }

        [Fact]
        public async Task Dashboard_ShouldSetWarningMessage_WhenNoData()
        {
            // Arrange
            var emptyModel = new DashboardViewModel
            {
                TotalUsers = 0,
                TotalFlights = 0,
                TotalAirlines = 0,
                TotalAirplanes = 0,
                TotalBookings = 0,
                FlightsByAirline = new Dictionary<string, int>(),
                RecentFlights = new List<FlightViewModel>(),
                RecentBookings = new List<BookingViewModel>(),
                MonthlyLabels = new List<string>(),
                MonthlyFlights = new List<int>()
            };

            _dashboardServiceMock
                .Setup(s => s.GetDashboardDataAsync())
                .ReturnsAsync(emptyModel);

            // Act
            var result = await _controller.Dashboard();

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<DashboardViewModel>(viewResult.Model);
            Assert.Equal(0, model.TotalFlights);
            Assert.Equal("No data available for dashboard.", _controller.ViewBag.WarningMessage);
            Assert.Null(_controller.ViewBag.ErrorMessage);
        }

        [Fact]
        public async Task Dashboard_ShouldSetErrorMessageAndReturnEmptyModel_WhenExceptionOccurs()
        {
            // Arrange
            _dashboardServiceMock
                .Setup(s => s.GetDashboardDataAsync())
                .ThrowsAsync(new Exception("Test exception"));

            // Act
            var result = await _controller.Dashboard();

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<DashboardViewModel>(viewResult.Model);
            Assert.Equal(0, model.TotalFlights);
            Assert.Equal("An error occurred while loading the dashboard. Please try again.", _controller.ViewBag.ErrorMessage);
            Assert.Null(_controller.ViewBag.WarningMessage);
        }

        #endregion

        #region GetDashboardData Action

        [Fact]
        public async Task GetDashboardData_ShouldReturnJsonWithModel_WhenSuccess()
        {
            // Arrange
            var dashboardModel = new DashboardViewModel
            {
                TotalUsers = 10,
                TotalFlights = 5,
                TotalAirlines = 3
            };

            _dashboardServiceMock
                .Setup(s => s.GetDashboardDataAsync())
                .ReturnsAsync(dashboardModel);

            // Act
            var result = await _controller.GetDashboardData();

            // Assert
            var jsonResult = Assert.IsType<JsonResult>(result);
            var jsonString = JsonSerializer.Serialize(jsonResult.Value);
            using var jsonDoc = JsonDocument.Parse(jsonString);
            var root = jsonDoc.RootElement;

            Assert.Equal(10, root.GetProperty("TotalUsers").GetInt32());
            Assert.Equal(5, root.GetProperty("TotalFlights").GetInt32());
            Assert.Equal(3, root.GetProperty("TotalAirlines").GetInt32());
        }

        [Fact]
        public async Task GetDashboardData_ShouldReturnErrorJson_WhenExceptionOccurs()
        {
            // Arrange
            _dashboardServiceMock
                .Setup(s => s.GetDashboardDataAsync())
                .ThrowsAsync(new Exception("Test exception"));

            // Act
            var result = await _controller.GetDashboardData();

            // Assert
            var jsonResult = Assert.IsType<JsonResult>(result);
            var jsonString = JsonSerializer.Serialize(jsonResult.Value);
            using var jsonDoc = JsonDocument.Parse(jsonString);
            var root = jsonDoc.RootElement;

            Assert.Equal("Failed to load data", root.GetProperty("error").GetString());
        }

        #endregion
    }
}


