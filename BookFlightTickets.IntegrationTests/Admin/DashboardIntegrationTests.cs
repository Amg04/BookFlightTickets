using BookFlightTickets.Core.ServiceContracts;
using BookFlightTickets.Core.ViewModels;
using BookFlightTickets.UI;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Net;
using System.Text.Json;

namespace BookFlightTickets.IntegrationTests.Admin
{
    public class DashboardIntegrationTests : BaseIntegrationTest
    {
        private readonly Mock<IDashboardService> _dashboardServiceMock;

        public DashboardIntegrationTests(WebApplicationFactory<Program> factory) : base(factory)
        {
            _dashboardServiceMock = new Mock<IDashboardService>();

            InitializeFactory(services =>
            {
                services.AddScoped(_ => _dashboardServiceMock.Object);
            });
        }

        #region Dashboard (GET)

        [Fact]
        public async Task Dashboard_Get_ReturnsViewWithData_WhenDataExists()
        {
            // Arrange
            var dashboardData = new DashboardViewModel
            {
                TotalFlights = 10,
                TotalBookings = 20,
                TotalRevenue = 5000,
                RecentFlights = new List<FlightViewModel>
                {
                    new FlightViewModel { Id = 1, Airline = new AirlineViewModel { Name = "Airline1" } }
                },
                RecentBookings = new List<BookingViewModel>
                {
                    new BookingViewModel { Id = 1, PNR = "ABC123" }
                }
            };

            _dashboardServiceMock.Setup(s => s.GetDashboardDataAsync())
                .ReturnsAsync(dashboardData);

            var client = CreateAdminClient();

            // Act
            var response = await client.GetAsync("/Admin/Dashboard/Dashboard");
            var content = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Airline1", content);
            Assert.Contains("RecentFlights", content);
        }

        [Fact]
        public async Task Dashboard_Get_ReturnsViewWithWarning_WhenNoData()
        {
            // Arrange
            var emptyData = new DashboardViewModel
            {
                TotalFlights = 0,
                TotalBookings = 0,
                TotalRevenue = 0,
                RecentFlights = new List<FlightViewModel>(),
                RecentBookings = new List<BookingViewModel>()
            };

            _dashboardServiceMock.Setup(s => s.GetDashboardDataAsync())
                .ReturnsAsync(emptyData);

            var client = CreateAdminClient();

            // Act
            var response = await client.GetAsync("/Admin/Dashboard/Dashboard");
            var content = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("toastr.warning('No data available for dashboard.');", content);
        }

        [Fact]
        public async Task Dashboard_Get_ReturnsViewWithError_WhenExceptionThrown()
        {
            // Arrange
            _dashboardServiceMock.Setup(s => s.GetDashboardDataAsync())
                .ThrowsAsync(new Exception("Service error"));

            var client = CreateAdminClient();

            // Act
            var response = await client.GetAsync("/Admin/Dashboard/Dashboard");
            var content = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("An error occurred while loading the dashboard", content);
        }

        [Fact]
        public async Task Dashboard_Get_UnauthorizedUser_ReturnsRedirectToLogin()
        {
            var client = CreateUnauthenticatedClient();
            var response = await client.GetAsync("/Admin/Dashboard/Dashboard");

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            var location = response.Headers.Location;
            Assert.NotNull(location);
            Assert.Equal("/Identity/Account/Login", location.LocalPath);
        }

        [Fact]
        public async Task Dashboard_Get_UserWithoutAdminRole_ReturnsForbidden()
        {
            var client = CreateAuthenticatedClient(); // regular authenticated user, not admin
            var response = await client.GetAsync("/Admin/Dashboard/Dashboard");

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        #endregion

        #region GetDashboardData (AJAX GET)

        [Fact]
        public async Task GetDashboardData_ReturnsJson_WhenDataExists()
        {
            // Arrange
            var dashboardData = new DashboardViewModel
            {
                TotalFlights = 5,
                TotalBookings = 15,
                TotalRevenue = 3000,
                RecentFlights = new List<FlightViewModel>(),
                RecentBookings = new List<BookingViewModel>()
            };

            _dashboardServiceMock.Setup(s => s.GetDashboardDataAsync())
                .ReturnsAsync(dashboardData);

            var client = CreateAdminClient();

            // Act
            var response = await client.GetAsync("/Admin/Dashboard/GetDashboardData");
            var content = await response.Content.ReadAsStringAsync();
            var json = JsonSerializer.Deserialize<DashboardViewModel>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.NotNull(json);
            Assert.Equal(5, json.TotalFlights);
            Assert.Equal(15, json.TotalBookings);
            Assert.Equal(3000, json.TotalRevenue);
        }

        [Fact]
        public async Task GetDashboardData_ReturnsJsonError_WhenExceptionThrown()
        {
            // Arrange
            _dashboardServiceMock.Setup(s => s.GetDashboardDataAsync())
                .ThrowsAsync(new Exception("Service error"));

            var client = CreateAdminClient();

            // Act
            var response = await client.GetAsync("/Admin/Dashboard/GetDashboardData");
            var content = await response.Content.ReadAsStringAsync();
            var json = JsonSerializer.Deserialize<Dictionary<string, string>>(content);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.NotNull(json);
            Assert.Contains("error", json.Keys);
            Assert.Equal("Failed to load data", json["error"]);
        }

        #endregion
    }
}