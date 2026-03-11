using BookFlightTickets.Core.Domain.Entities;
using BookFlightTickets.Core.Domain.Hubs;
using BookFlightTickets.Core.Domain.RepositoryContracts;
using BookFlightTickets.Core.ServiceContracts;
using BookFlightTickets.Core.ViewModels;
using BookFlightTickets.Infrastructure.Data.DbContext;
using BookFlightTickets.Infrastructure.Repositories;
using BookFlightTickets.UI;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BookFlightTickets.IntegrationTests.Admin
{
    public class FlightIntegrationTests : BaseIntegrationTest
    {
        private readonly Mock<IRedisCacheService> _cacheServiceMock;
        private readonly Mock<IHubContext<DashboardHub>> _hubContextMock;

        public FlightIntegrationTests(WebApplicationFactory<Program> factory) : base(factory)
        {
            _cacheServiceMock = new Mock<IRedisCacheService>();
            _hubContextMock = new Mock<IHubContext<DashboardHub>>();

            InitializeFactory(services =>
            {
                services.AddScoped(_ => _cacheServiceMock.Object);
                services.AddScoped(_ => _hubContextMock.Object);
            });
        }

        #region Index (GET)

        [Fact]
        public async Task Index_WhenFlightsExist_ReturnsViewWithFlights()
        {
            // Arrange
            await ExecuteWithDbContextAsync(async dbContext =>
            {
                var airline = new Airline { Name = "Test Airline", Code = "TA" };
                var airplane = new Airplane { Model = "Boeing 737", SeatCapacity = 180 };
                var airport1 = new Airport { Name = "JFK", Code = "JFK", City = "New York", Country = "USA" };
                var airport2 = new Airport { Name = "LAX", Code = "LAX", City = "Los Angeles", Country = "USA" };
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
                    BasePrice = 200,
                    Status = FlightStatus.Scheduled
                };
                dbContext.Flights.Add(flight);
                await dbContext.SaveChangesAsync();
            });

            _cacheServiceMock.Setup(c => c.GetOrSetAsync(
                It.IsAny<string>(),
                It.IsAny<Func<Task<List<FlightViewModel>>>>(),
                It.IsAny<TimeSpan?>()))
                .Returns((string key, Func<Task<List<FlightViewModel>>> factory, TimeSpan? expiry) => factory());

            var client = CreateAdminClient();

            // Act
            var response = await client.GetAsync("/Admin/Flight/Index");
            var content = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Test Airline", content);
            Assert.Contains("JFK", content);
            Assert.Contains("LAX", content);
        }

        [Fact]
        public async Task Index_WhenNoFlights_ReturnsViewWithInfoMessage()
        {
            _cacheServiceMock.Setup(c => c.GetOrSetAsync(
                It.IsAny<string>(),
                It.IsAny<Func<Task<List<FlightViewModel>>>>(),
                It.IsAny<TimeSpan?>()))
                .Returns((string key, Func<Task<List<FlightViewModel>>> factory, TimeSpan? expiry) => factory());

            var client = CreateAdminClient();

            // Act
            var response = await client.GetAsync("/Admin/Flight/Index");
            var content = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("No Flights Found!", content);
        }

        [Fact]
        public async Task Index_WhenExceptionThrown_DisplaysErrorMessage()
        {
            _cacheServiceMock.Setup(c => c.GetOrSetAsync(
                    It.IsAny<string>(),
                    It.IsAny<Func<Task<List<FlightViewModel>>>>(),
                    It.IsAny<TimeSpan?>()))
                .ThrowsAsync(new Exception("Cache error"));

            var client = CreateAdminClient();

            // Act
            var response = await client.GetAsync("/Admin/Flight/Index");
            var content = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("An error occurred while retrieving flights", content);
        }

        [Fact]
        public async Task Index_UnauthorizedUser_ReturnsRedirectToLogin()
        {
            var client = CreateUnauthenticatedClient();
            var response = await client.GetAsync("/Admin/Flight/Index");

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            var location = response.Headers.Location;
            Assert.NotNull(location);
            Assert.Equal("/Identity/Account/Login", location.LocalPath);
        }

        [Fact]
        public async Task Index_UserWithoutAdminRole_ReturnsForbidden()
        {
            var client = CreateAuthenticatedClient();
            var response = await client.GetAsync("/Admin/Flight/Index");

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        #endregion

        #region Create (GET)

        [Fact]
        public async Task Create_Get_ReturnsViewWithDropdowns()
        {
            await ExecuteWithDbContextAsync(async dbContext =>
            {
                dbContext.Airlines.Add(new Airline { Name = "Airline1", Code = "A1" });
                dbContext.Airlines.Add(new Airline { Name = "Airline2", Code = "A2" });
                dbContext.Airports.Add(new Airport { Name = "Airport1", Code = "AP1", City = "City1", Country = "Country1" });
                dbContext.Airports.Add(new Airport { Name = "Airport2", Code = "AP2", City = "City2", Country = "Country2" });
                await dbContext.SaveChangesAsync();
            });

            var client = CreateAdminClient();

            // Act
            var response = await client.GetAsync("/Admin/Flight/Create");
            var content = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Create Flight", content);
            Assert.Contains("Airline1", content);
            Assert.Contains("Airport1", content);
        }

        #endregion

        #region Create (POST)

        [Fact]
        public async Task Create_Post_ValidModel_RedirectsToIndexAndInvalidatesCache()
        {
            int airlineId = 0, airplaneId = 0, airport1Id = 0, airport2Id = 0;
            await ExecuteWithDbContextAsync(async dbContext =>
            {
                var airline = new Airline { Name = "Test Airline", Code = "TA" };
                var airplane = new Airplane { Model = "Boeing 737", SeatCapacity = 180 };
                var airport1 = new Airport { Name = "JFK", Code = "JFK", City = "NY", Country = "USA" };
                var airport2 = new Airport { Name = "LAX", Code = "LAX", City = "LA", Country = "USA" };
                dbContext.Airlines.Add(airline);
                dbContext.Airplanes.Add(airplane);
                dbContext.Airports.AddRange(airport1, airport2);
                await dbContext.SaveChangesAsync();
                airlineId = airline.Id;
                airplaneId = airplane.Id;
                airport1Id = airport1.Id;
                airport2Id = airport2.Id;
            });

            var client = Factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IUnitOfWork));
                    if (descriptor != null) services.Remove(descriptor);

                    services.AddScoped(serviceProvider =>
                    {
                        var dbContext = serviceProvider.GetRequiredService<BookFilghtsDbContext>();
                        var realUnitOfWork = new UnitOfWork(dbContext);

                        var mock = new Mock<IUnitOfWork>();

                        mock.Setup(u => u.Repository<Flight>()).Returns(realUnitOfWork.Repository<Flight>());
                        mock.Setup(u => u.Repository<Airline>()).Returns(realUnitOfWork.Repository<Airline>());
                        mock.Setup(u => u.Repository<Airplane>()).Returns(realUnitOfWork.Repository<Airplane>());
                        mock.Setup(u => u.Repository<Airport>()).Returns(realUnitOfWork.Repository<Airport>());
                        mock.Setup(u => u.Repository<FlightSeat>()).Returns(realUnitOfWork.Repository<FlightSeat>());

                        mock.Setup(u => u.CompleteAsync()).Returns(() => realUnitOfWork.CompleteAsync());

                        var transactionMock = new Mock<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction>();
                        mock.Setup(u => u.BeginTransactionAsync()).ReturnsAsync(transactionMock.Object);

                        return mock.Object;
                    });

                    services.AddScoped(_ => _cacheServiceMock.Object);
                    services.AddScoped(_ => _hubContextMock.Object);

                    services.AddAuthentication(options =>
                    {
                        options.DefaultAuthenticateScheme = "TestAdmin";
                        options.DefaultChallengeScheme = "TestAdmin";
                    }).AddScheme<AuthenticationSchemeOptions, TestAdminAuthHandler>("TestAdmin", _ => { });
                });
            }).CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

            var getResponse = await client.GetAsync("/Admin/Flight/Create");
            var getContent = await getResponse.Content.ReadAsStringAsync();
            var token = ExtractAntiForgeryToken(getContent);

            var formData = new Dictionary<string, string>
            {
                ["AirlineId"] = airlineId.ToString(),
                ["AirplaneId"] = airplaneId.ToString(),
                ["DepartureAirportID"] = airport1Id.ToString(),
                ["ArrivalAirportID"] = airport2Id.ToString(),
                ["DepartureTime"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm"),
                ["ArrivalTime"] = DateTime.UtcNow.AddHours(5).ToString("yyyy-MM-ddTHH:mm"),
                ["BasePrice"] = "250",
                ["Status"] = "Scheduled"
            };
            var content = new FormUrlEncodedContent(formData);
            content.Headers.Add("RequestVerificationToken", token);

            _cacheServiceMock.Setup(c => c.RemoveAsync(It.IsAny<string>())).Returns(Task.CompletedTask).Verifiable();

            var response = await client.PostAsync("/Admin/Flight/Create", content);

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal("/Admin/Flight", response.Headers.Location?.OriginalString);
            _cacheServiceMock.Verify(c => c.RemoveAsync("flights:all"), Times.AtLeastOnce);

            using (var scope = Factory.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<BookFilghtsDbContext>();
                var flight = await dbContext.Flights.FirstOrDefaultAsync(f => f.AirlineId == airlineId);
                Assert.NotNull(flight);
                Assert.Equal(airplaneId, flight.AirplaneId);
                Assert.Equal(airport1Id, flight.DepartureAirportID);
                Assert.Equal(airport2Id, flight.ArrivalAirportID);
                Assert.Equal(250, flight.BasePrice);
            }
        }

        [Fact]
        public async Task Create_Post_InvalidModel_ReturnsViewWithErrors()
        {
            var client = CreateAdminClient();

            var getResponse = await client.GetAsync("/Admin/Flight/Create");
            var getContent = await getResponse.Content.ReadAsStringAsync();
            var token = ExtractAntiForgeryToken(getContent);

            var formData = new Dictionary<string, string>
            {
                ["AirlineId"] = "",
                ["DepartureAirportID"] = "",
                ["ArrivalAirportID"] = "",
                ["BasePrice"] = "abc"
            };
            var content = new FormUrlEncodedContent(formData);
            content.Headers.Add("RequestVerificationToken", token);

            // Act
            var response = await client.PostAsync("/Admin/Flight/Create", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Create Flight", responseContent);
        }

        #endregion

        #region Edit (GET)

        [Fact]
        public async Task Edit_Get_ValidId_ReturnsViewWithFlight()
        {
            int flightId = 0;
            await ExecuteWithDbContextAsync(async dbContext =>
            {
                var airline = new Airline { Name = "Test Airline", Code = "TA" };
                var airplane = new Airplane { Model = "Boeing 737", SeatCapacity = 180 };
                var airport1 = new Airport { Name = "JFK", Code = "JFK", City = "NY", Country = "USA" };
                var airport2 = new Airport { Name = "LAX", Code = "LAX", City = "LA", Country = "USA" };
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
                    BasePrice = 200,
                    Status = FlightStatus.Scheduled
                };
                dbContext.Flights.Add(flight);
                await dbContext.SaveChangesAsync();
                flightId = flight.Id;
            });

            _cacheServiceMock.Setup(c => c.GetAsync<FlightViewModel>(It.IsAny<string>()))
                .ReturnsAsync((FlightViewModel)null);

            var client = CreateAdminClient();

            // Act
            var response = await client.GetAsync($"/Admin/Flight/Edit/{flightId}");
            var content = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Test Airline", content);
            Assert.Contains("JFK", content);
        }

        [Fact]
        public async Task Edit_Get_InvalidId_ReturnsNotFound()
        {
            var client = CreateAdminClient();
            var response = await client.GetAsync("/Admin/Flight/Edit/999");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task Edit_Get_WithoutId_ReturnsBadRequest()
        {
            var client = CreateAdminClient();
            var response = await client.GetAsync("/Admin/Flight/Edit/");

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        #endregion

        #region Edit (POST)

        [Fact]
        public async Task Edit_Post_ValidModel_RedirectsToIndexAndInvalidatesCache()
        {
            int flightId = 0;
            int airlineId, airplaneId, airport1Id, airport2Id;
            using (var scope = Factory.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<BookFilghtsDbContext>();
                await dbContext.Database.EnsureCreatedAsync();

                var airline = new Airline { Name = "Test Airline", Code = "TA" };
                var airplane = new Airplane { Model = "Boeing 737", SeatCapacity = 180 };
                var airport1 = new Airport { Name = "JFK", Code = "JFK", City = "NY", Country = "USA" };
                var airport2 = new Airport { Name = "LAX", Code = "LAX", City = "LA", Country = "USA" };
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
                    BasePrice = 200,
                    Status = FlightStatus.Scheduled
                };
                dbContext.Flights.Add(flight);
                await dbContext.SaveChangesAsync();
                flightId = flight.Id;
                airlineId = airline.Id;
                airplaneId = airplane.Id;
                airport1Id = airport1.Id;
                airport2Id = airport2.Id;
            }

            var client = CreateAdminClient();

            var getResponse = await client.GetAsync($"/Admin/Flight/Edit/{flightId}");
            var getContent = await getResponse.Content.ReadAsStringAsync();
            var token = ExtractAntiForgeryToken(getContent);

            var formData = new Dictionary<string, string>
            {
                ["Id"] = flightId.ToString(),
                ["AirlineId"] = airlineId.ToString(),
                ["AirplaneId"] = airplaneId.ToString(),
                ["DepartureAirportID"] = airport1Id.ToString(),
                ["ArrivalAirportID"] = airport2Id.ToString(),
                ["DepartureTime"] = DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-ddTHH:mm"),
                ["ArrivalTime"] = DateTime.UtcNow.AddDays(1).AddHours(5).ToString("yyyy-MM-ddTHH:mm"),
                ["BasePrice"] = "300",
                ["Status"] = "Scheduled"
            };
            var content = new FormUrlEncodedContent(formData);
            content.Headers.Add("RequestVerificationToken", token);

            _cacheServiceMock.Setup(c => c.RemoveAsync(It.IsAny<string>()))
                .Returns(Task.CompletedTask)
                .Verifiable();

            // Act
            var response = await client.PostAsync($"/Admin/Flight/Edit/{flightId}", content);

            // Assert
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal("/Admin/Flight", response.Headers.Location?.OriginalString);
            _cacheServiceMock.Verify(c => c.RemoveAsync("flights:all"), Times.AtLeastOnce);
            _cacheServiceMock.Verify(c => c.RemoveAsync($"flight:id:{flightId}"), Times.AtLeastOnce);

            using (var scope = Factory.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<BookFilghtsDbContext>();
                var flight = await dbContext.Flights.FindAsync(flightId);
                Assert.NotNull(flight);
                Assert.Equal(300, flight.BasePrice);
            }
        }

        [Fact]
        public async Task Edit_Post_FlightNotFound_ReturnsNotFound()
        {
            int flightId = 0;
            await ExecuteWithDbContextAsync(async dbContext =>
            {
                var airline = new Airline { Name = "Test Airline", Code = "TA" };
                var airplane = new Airplane { Model = "Boeing 737", SeatCapacity = 180 };
                var airport1 = new Airport { Name = "JFK", Code = "JFK", City = "NY", Country = "USA" };
                var airport2 = new Airport { Name = "LAX", Code = "LAX", City = "LA", Country = "USA" };
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
                    BasePrice = 200,
                    Status = FlightStatus.Scheduled
                };
                dbContext.Flights.Add(flight);
                await dbContext.SaveChangesAsync();
                flightId = flight.Id;
            });

            var client = CreateAdminClient();

            var getResponse = await client.GetAsync($"/Admin/Flight/Edit/{flightId}");
            var getContent = await getResponse.Content.ReadAsStringAsync();
            var token = ExtractAntiForgeryToken(getContent);

            int nonExistentId = 999;
            var formData = new Dictionary<string, string>
            {
                ["Id"] = nonExistentId.ToString(),
                ["AirlineId"] = "1",
                ["AirplaneId"] = "1",
                ["DepartureAirportID"] = "1",
                ["ArrivalAirportID"] = "2",
                ["DepartureTime"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm"),
                ["ArrivalTime"] = DateTime.UtcNow.AddHours(5).ToString("yyyy-MM-ddTHH:mm"),
                ["BasePrice"] = "300",
                ["Status"] = "Scheduled"
            };
            var content = new FormUrlEncodedContent(formData);
            content.Headers.Add("RequestVerificationToken", token);

            var response = await client.PostAsync($"/Admin/Flight/Edit/{nonExistentId}", content);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task Edit_Post_InvalidModel_ReturnsViewWithErrors()
        {
            int flightId = 0;
            await ExecuteWithDbContextAsync(async dbContext =>
            {
                var airline = new Airline { Name = "Test Airline", Code = "TA" };
                var airplane = new Airplane { Model = "Boeing 737", SeatCapacity = 180 };
                var airport1 = new Airport { Name = "JFK", Code = "JFK", City = "NY", Country = "USA" };
                var airport2 = new Airport { Name = "LAX", Code = "LAX", City = "LA", Country = "USA" };
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
                    BasePrice = 200,
                    Status = FlightStatus.Scheduled
                };
                dbContext.Flights.Add(flight);
                await dbContext.SaveChangesAsync();
                flightId = flight.Id;
            });

            var client = CreateAdminClient();

            var getResponse = await client.GetAsync($"/Admin/Flight/Edit/{flightId}");
            var getContent = await getResponse.Content.ReadAsStringAsync();
            var token = ExtractAntiForgeryToken(getContent);

            var formData = new Dictionary<string, string>
            {
                ["Id"] = flightId.ToString(),
                ["AirlineId"] = "",
                ["BasePrice"] = "abc"
            };
            var content = new FormUrlEncodedContent(formData);
            content.Headers.Add("RequestVerificationToken", token);

            // Act
            var response = await client.PostAsync($"/Admin/Flight/Edit/{flightId}", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Edit Flight", responseContent);
        }

        #endregion

        #region Details (GET)

        [Fact]
        public async Task Details_ValidId_ReturnsViewWithFlight()
        {
            int flightId = 0;
            await ExecuteWithDbContextAsync(async dbContext =>
            {
                var airline = new Airline { Name = "Test Airline", Code = "TA" };
                var airplane = new Airplane { Model = "Boeing 737", SeatCapacity = 180 };
                var airport1 = new Airport { Name = "JFK", Code = "JFK", City = "NY", Country = "USA" };
                var airport2 = new Airport { Name = "LAX", Code = "LAX", City = "LA", Country = "USA" };
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
                    BasePrice = 200,
                    Status = FlightStatus.Scheduled
                };
                dbContext.Flights.Add(flight);
                await dbContext.SaveChangesAsync();
                flightId = flight.Id;
            });

            _cacheServiceMock.Setup(c => c.GetAsync<FlightViewModel>(It.IsAny<string>()))
                .ReturnsAsync((FlightViewModel)null);

            var client = CreateAdminClient();

            // Act
            var response = await client.GetAsync($"/Admin/Flight/Details/{flightId}");
            var content = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Test Airline", content);
            Assert.Contains("JFK", content);
        }

        [Fact]
        public async Task Details_InvalidId_ReturnsNotFound()
        {
            var client = CreateAdminClient();
            var response = await client.GetAsync("/Admin/Flight/Details/999");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task Details_WithoutId_ReturnsBadRequest()
        {
            var client = CreateAdminClient();
            var response = await client.GetAsync("/Admin/Flight/Details/");

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        #endregion

        #region Delete (DELETE)

        [Fact]
        public async Task Delete_ValidId_ReturnsJsonSuccessAndInvalidatesCache()
        {
            int flightId = 0;
            await ExecuteWithDbContextAsync(async dbContext =>
            {
                var airline = new Airline { Name = "Test Airline", Code = "TA" };
                var airplane = new Airplane { Model = "Boeing 737", SeatCapacity = 180 };
                var airport1 = new Airport { Name = "JFK", Code = "JFK", City = "NY", Country = "USA" };
                var airport2 = new Airport { Name = "LAX", Code = "LAX", City = "LA", Country = "USA" };
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
                    BasePrice = 200,
                    Status = FlightStatus.Scheduled
                };
                dbContext.Flights.Add(flight);
                await dbContext.SaveChangesAsync();
                flightId = flight.Id;
            });

            _cacheServiceMock.Setup(c => c.RemoveAsync(It.IsAny<string>()))
                .Returns(Task.CompletedTask)
                .Verifiable();

            var client = CreateAdminClient();

            // Act
            var response = await client.DeleteAsync($"/Admin/Flight/Delete/{flightId}");
            var content = await response.Content.ReadAsStringAsync();
            var json = JsonSerializer.Deserialize<Dictionary<string, object>>(content);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(bool.Parse(json["success"].ToString()));
            Assert.Equal("Delete Successful", json["message"]?.ToString());
            _cacheServiceMock.Verify(c => c.RemoveAsync("flights:all"), Times.AtLeastOnce);
            _cacheServiceMock.Verify(c => c.RemoveAsync($"flight:id:{flightId}"), Times.AtLeastOnce);

            await ExecuteWithDbContextAsync(async dbContext =>
            {
                var flight = await dbContext.Flights.FindAsync(flightId);
                Assert.Null(flight);
            });
        }

        [Fact]
        public async Task Delete_InvalidId_ReturnsJsonNotFound()
        {
            var client = CreateAdminClient();
            var response = await client.DeleteAsync("/Admin/Flight/Delete/999");
            var content = await response.Content.ReadAsStringAsync();
            var json = JsonSerializer.Deserialize<Dictionary<string, object>>(content);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.False(bool.Parse(json["success"].ToString()));
            Assert.Equal("Flight not found", json["message"]?.ToString());
        }

        [Fact]
        public async Task Delete_WithoutId_ReturnsJsonError()
        {
            var client = CreateAdminClient();
            var response = await client.DeleteAsync("/Admin/Flight/Delete/");
            var content = await response.Content.ReadAsStringAsync();
            var json = JsonSerializer.Deserialize<Dictionary<string, object>>(content);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.False(bool.Parse(json["success"].ToString()));
            Assert.Equal("Invalid ID", json["message"]?.ToString());
        }

        #endregion

        #region GetAirplanesByAirlineId (AJAX)

        [Fact]
        public async Task GetAirplanesByAirlineId_ReturnsJsonList()
        {
            int airlineId = 0;
            await ExecuteWithDbContextAsync(async dbContext =>
            {
                var airline = new Airline { Name = "Test Airline", Code = "TA" };
                dbContext.Airlines.Add(airline);
                await dbContext.SaveChangesAsync();

                dbContext.Airplanes.Add(new Airplane { Model = "Boeing 737", SeatCapacity = 180, AirlineId = airline.Id });
                dbContext.Airplanes.Add(new Airplane { Model = "Airbus A320", SeatCapacity = 200, AirlineId = airline.Id });
                await dbContext.SaveChangesAsync();
                airlineId = airline.Id;
            });

            _cacheServiceMock.Setup(c => c.GetOrSetAsync<List<object>>(
                    It.IsAny<string>(),
                    It.IsAny<Func<Task<List<object>>>>(),
                    It.IsAny<TimeSpan?>()))
                .Returns((string key, Func<Task<List<object>>> factory, TimeSpan? expiry) => factory());

            var client = CreateAdminClient();

            // Act
            var response = await client.GetAsync($"/Admin/Flight/GetAirplanesByAirlineId?airlineId={airlineId}");
            var content = await response.Content.ReadAsStringAsync();
            var json = JsonSerializer.Deserialize<List<object>>(content);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.NotNull(json);
            Assert.Equal(2, json.Count);
        }

        [Fact]
        public async Task GetAirplanesByAirlineId_WhenException_ReturnsEmptyList()
        {
            _cacheServiceMock.Setup(c => c.GetOrSetAsync<List<object>>(
                    It.IsAny<string>(),
                    It.IsAny<Func<Task<List<object>>>>(),
                    It.IsAny<TimeSpan?>()))
                .ThrowsAsync(new Exception("Cache error"));

            var client = CreateAdminClient();

            var response = await client.GetAsync("/Admin/Flight/GetAirplanesByAirlineId?airlineId=1");
            var content = await response.Content.ReadAsStringAsync();
            var json = JsonSerializer.Deserialize<List<object>>(content);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.NotNull(json);
            Assert.Empty(json);
        }

        #endregion

        #region Helper method

        private string ExtractAntiForgeryToken(string htmlContent)
        {
            var regex = new Regex(@"<input[^>]*name=""__RequestVerificationToken""[^>]*value=""([^""]*)""");
            var match = regex.Match(htmlContent);
            if (match.Success)
            {
                return match.Groups[1].Value;
            }
            throw new Exception("Anti-forgery token not found in the HTML.");
        }

        #endregion
    }
}