using BookFlightTickets.Core.Domain.Entities;
using BookFlightTickets.Core.Domain.Enums;
using BookFlightTickets.Core.Domain.ResultPattern;
using BookFlightTickets.Core.ServiceContracts;
using BookFlightTickets.Core.ViewModels;
using BookFlightTickets.UI;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace BookFlightTickets.IntegrationTests.Customer
{
    public class FlightIntegrationTests : BaseIntegrationTest
    {
        private readonly Mock<IFlightService> _flightServiceMock;

        public FlightIntegrationTests(WebApplicationFactory<Program> factory) : base(factory)
        {
            _flightServiceMock = new Mock<IFlightService>();

            InitializeFactory(services =>
            {
                services.AddScoped(_ => _flightServiceMock.Object);
            });
        }

        #region Index - GET

        [Fact]
        public async Task Index_Get_AuthenticatedUser_ReturnsViewWithPagedFlights()
        {
            // Arrange
            var flights = new List<FlightViewModel>
            {
                new FlightViewModel { Id = 1, Airline = new AirlineViewModel { Name = "Airline1" } },
                new FlightViewModel { Id = 2, Airline = new AirlineViewModel { Name = "Airline2" } }
            };

            _flightServiceMock
                .Setup(s => s.GetFilteredFlights(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
                .ReturnsAsync(Result<List<FlightViewModel>>.Success(flights));

            _flightServiceMock
                .Setup(s => s.GetSortedFlightsAsync(It.IsAny<List<FlightViewModel>>(), It.IsAny<string>(), It.IsAny<SortOrderOptions>()))
                .ReturnsAsync(Result<List<FlightViewModel>>.Success(flights));

            var client = CreateAuthenticatedClient();

            // Act
            var response = await client.GetAsync("/Customer/Flight/Index");
            var content = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(StatusCodes.Status200OK, (int)response.StatusCode);
            Assert.Contains("Airline1", content);
            Assert.Contains("Airline2", content);
        }

        [Fact]
        public async Task Index_Get_UnauthenticatedUser_ReturnsViewWithPagedFlights()
        {
            // Arrange
            var flights = new List<FlightViewModel>
            {
                new FlightViewModel { Id = 1, Airline = new AirlineViewModel { Name = "Airline1" } },
                new FlightViewModel { Id = 2, Airline = new AirlineViewModel { Name = "Airline2" } }
            };

            _flightServiceMock
                .Setup(s => s.GetFilteredFlights(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
                .ReturnsAsync(Result<List<FlightViewModel>>.Success(flights));

            _flightServiceMock
                .Setup(s => s.GetSortedFlightsAsync(It.IsAny<List<FlightViewModel>>(), It.IsAny<string>(), It.IsAny<SortOrderOptions>()))
                .ReturnsAsync(Result<List<FlightViewModel>>.Success(flights));

            var client = CreateUnauthenticatedClient();

            // Act
            var response = await client.GetAsync("/Customer/Flight/Index");
            var content = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(StatusCodes.Status200OK, (int)response.StatusCode);
            Assert.Contains("Airline1", content);
            Assert.Contains("Airline2", content);
        }

        [Fact]
        public async Task Index_Get_WithSearchParameters_CallsServiceWithCorrectParameters()
        {
            // Arrange
            var flights = new List<FlightViewModel>
            {
                new FlightViewModel { Id = 1, Airline = new AirlineViewModel { Name = "Test Airline" } }
            };
            _flightServiceMock
                .Setup(s => s.GetFilteredFlights(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
                .ReturnsAsync(Result<List<FlightViewModel>>.Success(flights));
            _flightServiceMock
                .Setup(s => s.GetSortedFlightsAsync(It.IsAny<List<FlightViewModel>>(), It.IsAny<string>(), It.IsAny<SortOrderOptions>()))
                .ReturnsAsync(Result<List<FlightViewModel>>.Success(flights));

            var url = "/Customer/Flight/Index?SearchBy=Airline&SearchString=Test&FromDate=2025-01-01&ToDate=2025-01-31&SortBy=Price&SortOrder=ASC";
            var client = CreateAuthenticatedClient();

            // Act
            await client.GetAsync(url);

            // Assert
            _flightServiceMock.Verify(
                s => s.GetFilteredFlights("Airline", "Test", It.Is<DateTime?>(d => d == new DateTime(2025, 1, 1)), It.Is<DateTime?>(d => d == new DateTime(2025, 1, 31))),
                Times.Once);
            _flightServiceMock.Verify(
                s => s.GetSortedFlightsAsync(It.IsAny<List<FlightViewModel>>(), "Price", SortOrderOptions.ASC),
                Times.Once);
        }

        [Fact]
        public async Task Index_Get_WhenFromDateAfterToDate_DisplaysErrorMessageWithoutCallingService()
        {
            // Arrange
            var url = "/Customer/Flight/Index?FromDate=2025-02-01&ToDate=2025-01-01";
            var client = CreateAuthenticatedClient();

            // Act
            var response = await client.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(StatusCodes.Status200OK, (int)response.StatusCode);
            Assert.Contains("toastr.error('From date cannot be after To date');", content);
            _flightServiceMock.Verify(s => s.GetFilteredFlights(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()), Times.Never);
        }

        [Fact]
        public async Task Index_Get_WhenServiceReturnsFailure_DisplaysErrorMessage()
        {
            // Arrange
            _flightServiceMock
                .Setup(s => s.GetFilteredFlights(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
                .ReturnsAsync(Result<List<FlightViewModel>>.Failure(new Error("FLIGHT_001", "No flights found")));

            var client = CreateAuthenticatedClient();

            // Act
            var response = await client.GetAsync("/Customer/Flight/Index");
            var content = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(StatusCodes.Status200OK, (int)response.StatusCode);
            Assert.Contains("toastr.error('No flights found');", content);
        }

        [Fact]
        public async Task Index_Get_WhenExceptionThrown_DisplaysErrorMessage()
        {
            // Arrange
            _flightServiceMock
                .Setup(s => s.GetFilteredFlights(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
                .ThrowsAsync(new Exception("Database error"));

            var client = CreateAuthenticatedClient();

            // Act
            var response = await client.GetAsync("/Customer/Flight/Index");
            var content = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(StatusCodes.Status200OK, (int)response.StatusCode);
            Assert.Contains("An unexpected error occurred", content);
        }

        #endregion

        #region Details - GET

        [Fact]
        public async Task Details_ValidId_AuthenticatedUser_ReturnsViewWithFlight()
        {
            // Arrange
            int flightId = 0;
            await ExecuteWithDbContextAsync(async dbContext =>
            {
                await dbContext.Database.EnsureCreatedAsync();

                var airline = new Airline { Name = "Test Airline", Code = "TA" };
                var airplane = new Airplane { Model = "Boeing 737" };
                var airport1 = new Airport { Name = "JFK", Code = "JFK" };
                var airport2 = new Airport { Name = "LAX", Code = "LAX" };
                dbContext.Airlines.Add(airline);
                dbContext.Airplanes.Add(airplane);
                dbContext.Airports.AddRange(airport1, airport2);
                await dbContext.SaveChangesAsync();

                var flight = new Flight
                {
                    AirlineId = airline.Id,
                    AirplaneId = airplane.Id,
                    DepartureAirportID = airport1.Id,
                    ArrivalAirportID = airport2.Id,
                    DepartureTime = DateTime.UtcNow,
                    ArrivalTime = DateTime.UtcNow.AddHours(5),
                    BasePrice = 200
                };
                dbContext.Flights.Add(flight);
                await dbContext.SaveChangesAsync();
                flightId = flight.Id;
            });

            var client = CreateAuthenticatedClient();

            // Act
            var response = await client.GetAsync($"/Customer/Flight/Details/{flightId}");
            var content = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(StatusCodes.Status200OK, (int)response.StatusCode);
            Assert.Contains("Test Airline", content);
            Assert.Contains("JFK", content);
            Assert.Contains("LAX", content);
        }

        [Fact]
        public async Task Details_ValidId_UnauthenticatedUser_ReturnsViewWithFlight()
        {
            // Arrange
            int flightId = 0;
            await ExecuteWithDbContextAsync(async dbContext =>
            {
                await dbContext.Database.EnsureCreatedAsync();

                var airline = new Airline { Name = "Test Airline", Code = "TA" };
                var airplane = new Airplane { Model = "Boeing 737" };
                var airport1 = new Airport { Name = "JFK", Code = "JFK" };
                var airport2 = new Airport { Name = "LAX", Code = "LAX" };
                dbContext.Airlines.Add(airline);
                dbContext.Airplanes.Add(airplane);
                dbContext.Airports.AddRange(airport1, airport2);
                await dbContext.SaveChangesAsync();

                var flight = new Flight
                {
                    AirlineId = airline.Id,
                    AirplaneId = airplane.Id,
                    DepartureAirportID = airport1.Id,
                    ArrivalAirportID = airport2.Id,
                    DepartureTime = DateTime.UtcNow,
                    ArrivalTime = DateTime.UtcNow.AddHours(5),
                    BasePrice = 200
                };
                dbContext.Flights.Add(flight);
                await dbContext.SaveChangesAsync();
                flightId = flight.Id;
            });

            var client = CreateUnauthenticatedClient();

            // Act
            var response = await client.GetAsync($"/Customer/Flight/Details/{flightId}");
            var content = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(StatusCodes.Status200OK, (int)response.StatusCode);
            Assert.Contains("Test Airline", content);
            Assert.Contains("JFK", content);
            Assert.Contains("LAX", content);
        }

        [Fact]
        public async Task Details_InvalidId_AuthenticatedUser_ReturnsNotFound()
        {
            var client = CreateAuthenticatedClient();

            // Act
            var response = await client.GetAsync("/Customer/Flight/999");

            // Assert
            Assert.Equal(StatusCodes.Status404NotFound, (int)response.StatusCode);
        }

        [Fact]
        public async Task Details_InvalidId_UnauthenticatedUser_ReturnsNotFound()
        {
            var client = CreateUnauthenticatedClient();

            // Act
            var response = await client.GetAsync("/Customer/Flight/999");

            // Assert
            Assert.Equal(StatusCodes.Status404NotFound, (int)response.StatusCode);
        }

        #endregion
    }
}