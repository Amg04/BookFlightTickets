using BookFlightTickets.Core.Domain.Entities;
using BookFlightTickets.Core.Domain.RepositoryContracts;
using BookFlightTickets.Core.Services;
using BookFlightTickets.Infrastructure.Data.DbContext;
using BookFlightTickets.Infrastructure.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace BookFlightTickets.ServiceTests
{
    public class DashboardServiceTests : IDisposable
    {
        private readonly BookFilghtsDbContext _context;
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserManager<AppUser> _userManager;
        private readonly ILogger<DashboardService> _loggerMock;
        private readonly DashboardService _service;
        private readonly string _databaseName;

        public DashboardServiceTests()
        {
            _databaseName = Guid.NewGuid().ToString();

            var options = new DbContextOptionsBuilder<BookFilghtsDbContext>()
                .UseInMemoryDatabase(databaseName: _databaseName).Options;

            _context = new BookFilghtsDbContext(options);

            var userStore = new UserStore<AppUser>(_context);
            _userManager = new UserManager<AppUser>(
                userStore, null, new PasswordHasher<AppUser>(), null, null, null, null, null, null);

            _unitOfWork = new UnitOfWork(_context); 
            _loggerMock = Mock.Of<ILogger<DashboardService>>();
            _service = new DashboardService(_unitOfWork, _userManager, _loggerMock);
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        private async Task SeedDataAsync(
            List<AppUser>? users = null,
            List<Airline>? airlines = null,
            List<Airplane>? airplanes = null,
            List<Flight>? flights = null,
            List<Booking>? bookings = null)
        {
            if (users != null)
            {
                foreach (var user in users)
                {
                    await _userManager.CreateAsync(user);
                }
            }

            if (airlines != null)
            {
                await _unitOfWork.Repository<Airline>().AddRangeAsync(airlines);
            }

            if (airplanes != null)
            {
                await _unitOfWork.Repository<Airplane>().AddRangeAsync(airplanes);
            }

            if (flights != null)
            {
                await _unitOfWork.Repository<Flight>().AddRangeAsync(flights);
            }

            if (bookings != null)
            {
                await _unitOfWork.Repository<Booking>().AddRangeAsync(bookings);
            }

            await _unitOfWork.CompleteAsync();
        }

        [Fact]
        public async Task GetDashboardDataAsync_ShouldReturnDashboardViewModel_WhenDataExists()
        {
            // Arrange
            var users = new List<AppUser>
            {
                new AppUser { UserName = "user1", Email = "user1@test.com" , FirstName="Ali"},
                new AppUser { UserName = "user2", Email = "user2@test.com" , FirstName="Ahmed"}
            };

            var airlines = new List<Airline>
            {
                new Airline { Name = "Airline A", Code = "AA" },
                new Airline { Name = "Airline B", Code = "AB" }
            };

            var airplanes = new List<Airplane>
            {
                new Airplane { Model = "Boeing 737", SeatCapacity = 180, Airline = airlines[0] },
                new Airplane { Model = "Airbus A320", SeatCapacity = 200, Airline = airlines[1] }
            };

            var flights = new List<Flight>
            {
                new Flight
                {
                    Airline = airlines[0],
                    Airplane = airplanes[0],
                    DepartureAirport = new Airport { Name = "JFK", Code = "JFK", City = "New York", Country = "USA" },
                    ArrivalAirport = new Airport { Name = "LHR", Code = "LHR", City = "London", Country = "UK" },
                    DepartureTime = DateTime.UtcNow.AddHours(2),
                    ArrivalTime = DateTime.UtcNow.AddHours(10),
                    BasePrice = 500,
                    Status = FlightStatus.Scheduled
                },
                new Flight
                {
                    Airline = airlines[1],
                    Airplane = airplanes[1],
                    DepartureAirport = new Airport { Name = "LHR", Code = "LHR", City = "London", Country = "UK" },
                    ArrivalAirport = new Airport { Name = "CDG", Code = "CDG", City = "Paris", Country = "France" },
                    DepartureTime = DateTime.UtcNow.AddHours(5),
                    ArrivalTime = DateTime.UtcNow.AddHours(7),
                    BasePrice = 400,
                    Status = FlightStatus.Scheduled
                }
            };

            var bookings = new List<Booking>
            {
                new Booking
                {
                    AppUser = users[0],
                    Flight = flights[0],
                    BookingDate = DateTime.UtcNow.AddDays(-1),
                    PNR = "ABC123",
                    TotalPrice = 600,
                    Status = Status.Confirmed,
                    LastUpdated = DateTime.UtcNow,
                    Payment = new Payment { Amount = 600, PaymentDate = DateTime.UtcNow }
                },
                new Booking
                {
                    AppUser = users[1],
                    Flight = flights[1],
                    BookingDate = DateTime.UtcNow.AddDays(-2),
                    PNR = "XYZ789",
                    TotalPrice = 450,
                    Status = Status.Pending,
                    LastUpdated = DateTime.UtcNow,
                    Payment = null
                }
            };

            await SeedDataAsync(users, airlines, airplanes, flights, bookings);

            // Act
            var result = await _service.GetDashboardDataAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.TotalUsers);
            Assert.Equal(2, result.TotalFlights);
            Assert.Equal(2, result.TotalAirlines);
            Assert.Equal(2, result.TotalAirplanes);
            Assert.Equal(2, result.TotalBookings);
            Assert.Equal(2, result.FlightsByAirline.Count);
            Assert.Equal(2, result.RecentFlights.Count());
            Assert.Equal(2, result.RecentBookings.Count());
            Assert.Equal(6, result.MonthlyLabels.Count);
            Assert.Equal(6, result.MonthlyFlights.Count);
        }

        [Fact]
        public async Task GetDashboardDataAsync_ShouldReturnZeroValues_WhenNoData()
        {
            await SeedDataAsync();

            // Act
            var result = await _service.GetDashboardDataAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(0, result.TotalUsers);
            Assert.Equal(0, result.TotalFlights);
            Assert.Equal(0, result.TotalAirlines);
            Assert.Equal(0, result.TotalAirplanes);
            Assert.Equal(0, result.TotalBookings);
            Assert.Empty(result.FlightsByAirline);
            Assert.Empty(result.RecentFlights);
            Assert.Empty(result.RecentBookings);
            Assert.Equal(6, result.MonthlyLabels.Count);
            Assert.Equal(6, result.MonthlyFlights.Count);
            Assert.All(result.MonthlyFlights, v => Assert.Equal(0, v));
        }

        [Fact]
        public async Task GetDashboardDataAsync_ShouldHandleNullFromFlightCountByAirline()
        {
            // Arrange
            var users = new List<AppUser> { new AppUser { UserName = "user1", Email = "user1@test.com" , FirstName = "salah"} };
            var airlines = new List<Airline>
            {
                new Airline { Name = "Airline A", Code = "AA" }
            };
            var airplanes = new List<Airplane>
            {
                new Airplane { Model = "Boeing 737", SeatCapacity = 180, Airline = airlines[0] }
            };
            var flights = new List<Flight>
            {
                new Flight
                {
                    Airline = airlines[0],
                    Airplane = airplanes[0],
                    DepartureAirport = new Airport { Name = "JFK", Code = "JFK", City = "New York", Country = "USA" },
                    ArrivalAirport = new Airport { Name = "LHR", Code = "LHR", City = "London", Country = "UK" },
                    DepartureTime = DateTime.UtcNow.AddHours(2),
                    ArrivalTime = DateTime.UtcNow.AddHours(10),
                    BasePrice = 500,
                    Status = FlightStatus.Scheduled
                }
            };
            var bookings = new List<Booking>
            {
                new Booking
                {
                    AppUser = users[0],
                    Flight = flights[0],
                    BookingDate = DateTime.UtcNow.AddDays(-1),
                    PNR = "ABC123",
                    TotalPrice = 600,
                    Status = Status.Confirmed,
                    LastUpdated = DateTime.UtcNow,
                    Payment = new Payment { Amount = 600, PaymentDate = DateTime.UtcNow }
                }
            };

            await SeedDataAsync(users, airlines, airplanes, flights, bookings);

            // Act
            var result = await _service.GetDashboardDataAsync();

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.FlightsByAirline);
            Assert.NotEmpty(result.FlightsByAirline); 
            Assert.Equal(1, result.TotalUsers);
            Assert.Equal(1, result.TotalFlights);
        }
    }
}