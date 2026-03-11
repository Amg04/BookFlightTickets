using BookFlightTickets.Core.Domain.Entities;
using BookFlightTickets.Core.Domain.RepositoryContracts;
using BookFlightTickets.Core.Domain.Specifications;
using BookFlightTickets.Core.ViewModels;
using BookFlightTickets.UI.Areas.Admin.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging;
using Moq;

namespace BookFlightTickets.ControllerTests.Admin
{
    public class BookingsControllerTests
    {
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly Mock<IGenericRepository<Booking>> _repositoryMock;
        private readonly Mock<ILogger<BookingsController>> _loggerMock;
        private readonly BookingsController _controller;
        private readonly ITempDataDictionary _tempData;

        public BookingsControllerTests()
        {
            _unitOfWorkMock = new Mock<IUnitOfWork>();
            _repositoryMock = new Mock<IGenericRepository<Booking>>();
            _loggerMock = new Mock<ILogger<BookingsController>>();

            // Setup UnitOfWork to return the mocked repository
            _unitOfWorkMock.Setup(u => u.Repository<Booking>()).Returns(_repositoryMock.Object);

            // Create controller instance
            _controller = new BookingsController(
                _unitOfWorkMock.Object,
                _loggerMock.Object
            );

            // Setup TempData (not strictly needed for Index, but for consistency)
            _tempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>());
            _controller.TempData = _tempData;
        }

        #region Index

        [Fact]
        public async Task Index_ShouldReturnViewWithBookings_WhenBookingsExist()
        {
            // Arrange
            var bookingEntities = new List<Booking>
            {
                new Booking
                {
                    Id = 1,
                    UserId = "user1",
                    AppUser = new AppUser { Id = "user1", UserName = "john.doe", Email = "john@example.com" },
                    FlightId = 101,
                    Flight = new Flight
                    {
                        Id = 101,
                        AirlineId = 1,
                        Airline = new Airline { Id = 1, Name = "Test Airlines", Code = "TA" },
                        AirplaneId = 1,
                        Airplane = new Airplane { Id = 1, Model = "Boeing 737", SeatCapacity = 180, AirlineId = 1 },
                        DepartureAirportID = 1,
                        DepartureAirport = new Airport { Id = 1, Name = "JFK", Code = "JFK", City = "New York", Country = "USA" },
                        ArrivalAirportID = 2,
                        ArrivalAirport = new Airport { Id = 2, Name = "Heathrow", Code = "LHR", City = "London", Country = "UK" },
                        DepartureTime = DateTime.UtcNow.AddDays(-5).AddHours(10),
                        ArrivalTime = DateTime.UtcNow.AddDays(-5).AddHours(20),
                        BasePrice = 500,
                        Status = FlightStatus.Scheduled
                    },
                    BookingDate = DateTime.UtcNow.AddDays(-5),
                    PNR = "ABC123",
                    TotalPrice = 100,
                    Status = Status.Confirmed,
                    LastUpdated = DateTime.UtcNow,
                    Payment = new Payment { Id = 1, Amount = 100, PaymentDate = DateTime.UtcNow }
                },
                new Booking
                {
                    Id = 2,
                    UserId = "user2",
                    AppUser = new AppUser { Id = "user2", UserName = "jane.smith", Email = "jane@example.com" },
                    FlightId = 102,
                    Flight = new Flight
                    {
                        Id = 102,
                        AirlineId = 2,
                        Airline = new Airline { Id = 2, Name = "Another Airlines", Code = "AA" },
                        AirplaneId = 2,
                        Airplane = new Airplane { Id = 2, Model = "Airbus A320", SeatCapacity = 200, AirlineId = 2 },
                        DepartureAirportID = 2,
                        DepartureAirport = new Airport { Id = 2, Name = "Heathrow", Code = "LHR", City = "London", Country = "UK" },
                        ArrivalAirportID = 3,
                        ArrivalAirport = new Airport { Id = 3, Name = "Charles de Gaulle", Code = "CDG", City = "Paris", Country =  "France" },
                        DepartureTime = DateTime.UtcNow.AddDays(-3).AddHours(8),
                        ArrivalTime = DateTime.UtcNow.AddDays(-3).AddHours(10),
                        BasePrice = 300,
                        Status = FlightStatus.Scheduled
                    },
                    BookingDate = DateTime.UtcNow.AddDays(-3),
                    PNR = "XYZ789",
                    TotalPrice = 200,
                    Status = Status.Pending,
                    LastUpdated = DateTime.UtcNow,
                    Payment = new Payment { Id = 2, Amount = 200, PaymentDate = DateTime.UtcNow }
                }
            };
            _repositoryMock
                .Setup(r => r.GetAllWithSpecAsync(It.IsAny<BaseSpecification<Booking>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(bookingEntities);

            // Act
            var result = await _controller.Index();

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<IEnumerable<BookingViewModel>>(viewResult.Model);
            Assert.Equal(2, model.Count());
            _repositoryMock.Verify(
                r => r.GetAllWithSpecAsync(It.IsAny<BaseSpecification<Booking>>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task Index_ShouldReturnViewWithInfoMessage_WhenNoBookings()
        {
            // Arrange
            var bookingEntities = new List<Booking>(); // empty list

            _repositoryMock
                .Setup(r => r.GetAllWithSpecAsync(It.IsAny<BaseSpecification<Booking>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(bookingEntities);

            // Act
            var result = await _controller.Index();

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<IEnumerable<BookingViewModel>>(viewResult.Model);
            Assert.Empty(model);
            Assert.Equal("No bookings available.", _controller.ViewBag.InfoMessage);
        }

        [Fact]
        public async Task Index_ShouldReturnViewWithInfoMessage_WhenBookingsIsNull()
        {
            // Arrange
            _repositoryMock
                .Setup(r => r.GetAllWithSpecAsync(It.IsAny<BaseSpecification<Booking>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((List<Booking>)null); // null result

            // Act
            var result = await _controller.Index();

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<IEnumerable<BookingViewModel>>(viewResult.Model);
            Assert.Empty(model);
            Assert.Equal("No bookings available.", _controller.ViewBag.InfoMessage);
        }

        [Fact]
        public async Task Index_ShouldReturnViewWithError_WhenExceptionOccurs()
        {
            // Arrange
            _repositoryMock
                .Setup(r => r.GetAllWithSpecAsync(It.IsAny<BaseSpecification<Booking>>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await _controller.Index();

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<IEnumerable<BookingViewModel>>(viewResult.Model);
            Assert.Empty(model);
            Assert.Equal("An error occurred while retrieving bookings.", _controller.ViewBag.ErrorMessage);
        }

        #endregion
    }
}