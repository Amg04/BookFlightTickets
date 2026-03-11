using BookFlightTickets.Core.Domain.Entities;
using BookFlightTickets.Core.Domain.Hubs;
using BookFlightTickets.Core.Domain.RepositoryContracts;
using BookFlightTickets.Core.Domain.Specifications;
using BookFlightTickets.Core.ServiceContracts;
using BookFlightTickets.Core.Shared.Utility;
using BookFlightTickets.Core.ViewModels;
using BookFlightTickets.UI.Areas.Admin.Controllers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
namespace BookFlightTickets.ControllerTests.Admin
{
    public class FlightControllerTests
    {
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly Mock<IGenericRepository<Flight>> _flightRepoMock;
        private readonly Mock<IGenericRepository<Airline>> _airlineRepoMock;
        private readonly Mock<IGenericRepository<Airplane>> _airplaneRepoMock;
        private readonly Mock<IGenericRepository<Airport>> _airportRepoMock;
        private readonly Mock<IGenericRepository<FlightSeat>> _flightSeatRepoMock;
        private readonly Mock<IRedisCacheService> _cacheServiceMock;
        private readonly Mock<IWebHostEnvironment> _envMock;
        private readonly Mock<ILogger<FlightController>> _loggerMock;
        private readonly Mock<IHubContext<DashboardHub>> _hubContextMock;
        private readonly FlightController _controller;
        private readonly ITempDataDictionary _tempData;

        public FlightControllerTests()
        {
            _unitOfWorkMock = new Mock<IUnitOfWork>();
            _flightRepoMock = new Mock<IGenericRepository<Flight>>();
            _airlineRepoMock = new Mock<IGenericRepository<Airline>>();
            _airplaneRepoMock = new Mock<IGenericRepository<Airplane>>();
            _airportRepoMock = new Mock<IGenericRepository<Airport>>();
            _flightSeatRepoMock = new Mock<IGenericRepository<FlightSeat>>();
            _cacheServiceMock = new Mock<IRedisCacheService>();
            _envMock = new Mock<IWebHostEnvironment>();
            _loggerMock = new Mock<ILogger<FlightController>>();
            _hubContextMock = new Mock<IHubContext<DashboardHub>>();

            // Setup UnitOfWork to return mocked repositories
            _unitOfWorkMock.Setup(u => u.Repository<Flight>()).Returns(_flightRepoMock.Object);
            _unitOfWorkMock.Setup(u => u.Repository<Airline>()).Returns(_airlineRepoMock.Object);
            _unitOfWorkMock.Setup(u => u.Repository<Airplane>()).Returns(_airplaneRepoMock.Object);
            _unitOfWorkMock.Setup(u => u.Repository<Airport>()).Returns(_airportRepoMock.Object);
            _unitOfWorkMock.Setup(u => u.Repository<FlightSeat>()).Returns(_flightSeatRepoMock.Object);

            // Create controller instance
            _controller = new FlightController(
                _unitOfWorkMock.Object,
                _envMock.Object,
                _cacheServiceMock.Object,
                _loggerMock.Object,
                _hubContextMock.Object
            );

            // Setup TempData
            _tempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>());
            _controller.TempData = _tempData;
        }

        #region Index

        [Fact]
        public async Task Index_ShouldReturnViewWithFlights_WhenCacheHit()
        {
            // Arrange
            var flights = new List<FlightViewModel>
            {
                new FlightViewModel { Id = 1 },
                new FlightViewModel { Id = 2 }
            };

            _cacheServiceMock.Setup(c => c.GetOrSetAsync(
                    It.Is<string>(key => key == "flights:all"),
                    It.IsAny<Func<Task<List<FlightViewModel>>>>(),
                    It.IsAny<TimeSpan>()))
                .ReturnsAsync(flights);

            // Act
            var result = await _controller.Index();

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<IEnumerable<FlightViewModel>>(viewResult.Model);
            Assert.Equal(2, model.Count());
            _cacheServiceMock.Verify(c => c.GetOrSetAsync("flights:all", It.IsAny<Func<Task<List<FlightViewModel>>>>(), It.IsAny<TimeSpan>()), Times.Once);
        }

        [Fact]
        public async Task Index_ShouldReturnViewWithFlights_WhenCacheMiss()
        {
            // Arrange
            var flightEntities = new List<Flight>
            {
                new Flight { Id = 1, Airline = new Airline(), Airplane = new Airplane(), DepartureAirport = new Airport(), ArrivalAirport = new Airport() },
                new Flight { Id = 2 , Airline = new Airline(), Airplane = new Airplane(), DepartureAirport = new Airport(), ArrivalAirport = new Airport()}
            };

            _unitOfWorkMock
               .Setup(u => u.Repository<Flight>())
               .Returns(_flightRepoMock.Object);

            _flightRepoMock
                .Setup(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<Flight>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(flightEntities);

            _cacheServiceMock
            .Setup(c => c.GetOrSetAsync(
                "flights:all",
                It.IsAny<Func<Task<List<FlightViewModel>>>>(),
                It.IsAny<TimeSpan>()))
            .ReturnsAsync((string key, Func<Task<List<FlightViewModel>>> factory, TimeSpan expiry) => factory().Result);

            // Act
            var result = await _controller.Index();

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<IEnumerable<FlightViewModel>>(viewResult.Model);
            Assert.Equal(2, model.Count());
            _flightRepoMock.Verify(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<Flight>>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Index_ShouldReturnViewWithInfoMessage_WhenNoFlights()
        {
            // Arrange
            _cacheServiceMock.Setup(c => c.GetOrSetAsync(
                    It.IsAny<string>(),
                    It.IsAny<Func<Task<List<FlightViewModel>>>>(),
                    It.IsAny<TimeSpan>()))
                .ReturnsAsync(new List<FlightViewModel>());

            // Act
            var result = await _controller.Index();

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<IEnumerable<FlightViewModel>>(viewResult.Model);
            Assert.Empty(model);
            Assert.Equal("No flights available.", _controller.ViewBag.InfoMessage);
        }

        [Fact]
        public async Task Index_ShouldReturnViewWithError_WhenExceptionOccurs()
        {
            // Arrange
            _cacheServiceMock.Setup(c => c.GetOrSetAsync(
                    It.IsAny<string>(),
                    It.IsAny<Func<Task<List<FlightViewModel>>>>(),
                    It.IsAny<TimeSpan>()))
                .ThrowsAsync(new Exception("Test exception"));

            // Act
            var result = await _controller.Index();

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<IEnumerable<FlightViewModel>>(viewResult.Model);
            Assert.Empty(model);
            Assert.Equal("An error occurred while retrieving flights.", _controller.ViewBag.ErrorMessage);
        }

        #endregion

        #region Create (GET)

        [Fact]
        public async Task Create_Get_ShouldReturnViewWithDropDowns()
        {
            // Arrange
            var airlines = new List<Airline> { new Airline { Id = 1, Name = "A" } };
            var airports = new List<Airport> { new Airport { Id = 1, Name = "JFK" } };

            _airlineRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(airlines);
            _airportRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(airports);

            // Act
            var result = await _controller.Create();

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.NotNull(_controller.ViewBag.Airlines);
            Assert.NotNull(_controller.ViewBag.Airports);
        }

        [Fact]
        public async Task Create_Get_WhenException_ShouldRedirectToIndexWithError()
        {
            // Arrange
            _airlineRepoMock.Setup(r => r.GetAllAsync()).ThrowsAsync(new Exception("Database error"));
            _airportRepoMock.Setup(r => r.GetAllAsync()).ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await _controller.Create();

            // Assert
            var redirectResult = Assert.IsType<ViewResult>(result);
            Assert.NotNull(_controller.ViewBag.Airlines);
            Assert.NotNull(_controller.ViewBag.Airports);
            Assert.Empty(_controller.ViewBag.Airlines);
            Assert.Empty(_controller.ViewBag.Airports);
            Assert.Null(_controller.TempData["error"]);
        }

        #endregion

        #region Create (POST)

        [Fact]
        public async Task Create_Post_InvalidModel_ShouldReturnViewWithModelAndDropDowns()
        {
            // Arrange
            var flightVM = new FlightViewModel { AirlineId = 1, DepartureAirportID = 2, ArrivalAirportID = 3 };
            _controller.ModelState.AddModelError("FlightNumber", "Required");

            var airlines = new List<Airline> { new Airline { Id = 1, Name = "A" } };
            var airports = new List<Airport> { new Airport { Id = 2, Name = "JFK" }, new Airport { Id = 3, Name = "LHR" } };
            var airplanes = new List<Airplane> { new Airplane { Id = 10, Model = "B737" } };

            _airlineRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(airlines);
            _airportRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(airports);
            _cacheServiceMock.Setup(c => c.GetOrSetAsync(
                    It.IsAny<string>(),
                    It.IsAny<Func<Task<List<Airplane>>>>(),
                    It.IsAny<TimeSpan>()))
                .ReturnsAsync(airplanes);

            // Act
            var result = await _controller.Create(flightVM);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal(flightVM, viewResult.Model);
            Assert.NotNull(_controller.ViewBag.Airlines);
            Assert.NotNull(_controller.ViewBag.Airports);
            _flightRepoMock.Verify(r => r.AddAsync(It.IsAny<Flight>()), Times.Never);
        }

        [Fact]
        public async Task Create_Post_ValidModel_ShouldAddFlightAndSeats_AndInvalidateCache_AndSendHubNotification()
        {
            // Arrange
            var flightVM = new FlightViewModel
            {
                Id = 0,
                AirlineId = 1,
                AirplaneId = 2,
                DepartureAirportID = 3,
                ArrivalAirportID = 4,
                DepartureTime = DateTime.UtcNow.AddDays(1),
                ArrivalTime = DateTime.UtcNow.AddDays(1).AddHours(2),
                BasePrice = 500,
                Status = FlightStatus.Scheduled
            };

            Flight capturedFlight = null;
            _flightRepoMock.Setup(r => r.AddAsync(It.IsAny<Flight>()))
                .Callback<Flight>(f =>
                {
                    capturedFlight = f;
                    f.Id = 10;
                })
                .Returns(Task.CompletedTask);

            _unitOfWorkMock.Setup(u => u.CompleteAsync()).ReturnsAsync(1);

            // Setup airplane with seat templates
            var seatTemplates = new List<Seat>
            {
                new Seat { Id = 1,AirplaneId = 1 ,Row = "1A" , Number = 3,Class = SeatClass.Economy,Price = 60},
                new Seat { Id = 2,AirplaneId = 1 , Row = "1B" , Number = 4, Class = SeatClass.Business, Price = 80}
            };

            var airplane = new Airplane { Id = 2, SeatTemplates = seatTemplates };
            _airplaneRepoMock.Setup(r => r.GetEntityWithSpecAsync(It.IsAny<ISpecification<Airplane>>()))
                .ReturnsAsync(airplane);

            _flightSeatRepoMock.Setup(r => r.AddAsync(It.IsAny<FlightSeat>())).Returns(Task.CompletedTask);
            _flightSeatRepoMock.Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<FlightSeat>>())).Returns(Task.CompletedTask);
            _unitOfWorkMock.SetupSequence(u => u.CompleteAsync())
                .ReturnsAsync(1)
                .ReturnsAsync(seatTemplates.Count);

            var transactionMock = new Mock<IDbContextTransaction>();
            _unitOfWorkMock.Setup(u => u.BeginTransactionAsync())
                .ReturnsAsync(transactionMock.Object);

            // Cache invalidation
            _cacheServiceMock.Setup(c => c.RemoveAsync("flights:all")).Returns(Task.CompletedTask);
            _cacheServiceMock.Setup(c => c.RemoveByPatternAsync("flight:id:*")).Returns(Task.CompletedTask);
            _cacheServiceMock.Setup(c => c.RemoveByPatternAsync("flight:details:id:*")).Returns(Task.CompletedTask);
            _cacheServiceMock.Setup(c => c.RemoveByPatternAsync("airplanes:flight:*")).Returns(Task.CompletedTask);

            // Hub
            var clientsMock = new Mock<IHubClients>();
            var clientProxyMock = new Mock<IClientProxy>();
            clientsMock.Setup(c => c.Group(SD.Admin)).Returns(clientProxyMock.Object);
            _hubContextMock.Setup(h => h.Clients).Returns(clientsMock.Object);
            clientProxyMock.Setup(c => c.SendCoreAsync("ReceiveUpdate", It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _controller.ModelState.Clear();

            // Act
            var result = await _controller.Create(flightVM);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(nameof(FlightController.Index), redirectResult.ActionName);
            Assert.Equal("Flight has been Added Successfully", _controller.TempData["success"]);

            _flightRepoMock.Verify(r => r.AddAsync(It.Is<Flight>(f =>
                 f.AirlineId == flightVM.AirlineId &&
                 f.AirplaneId == flightVM.AirplaneId &&
                 f.DepartureAirportID == flightVM.DepartureAirportID)), Times.Once);

            Assert.NotNull(capturedFlight);
            Assert.Equal(10, capturedFlight.Id);
            _unitOfWorkMock.Verify(u => u.CompleteAsync(), Times.Exactly(2));
            _airplaneRepoMock.Verify(r => r.GetEntityWithSpecAsync(It.IsAny<ISpecification<Airplane>>()), Times.Once);
            _flightSeatRepoMock.Verify(r => r.AddRangeAsync(It.IsAny<IEnumerable<FlightSeat>>()), Times.Once); 
            _cacheServiceMock.Verify(c => c.RemoveAsync("flights:all"), Times.Once);
            _cacheServiceMock.Verify(c => c.RemoveByPatternAsync("airplanes:flight:*"), Times.Once);
            clientProxyMock.Verify(c => c.SendCoreAsync("ReceiveUpdate", It.IsAny<object[]>(), It.IsAny<CancellationToken>()), Times.Once);
            transactionMock.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Create_Post_WhenSaveFails_ShouldReturnViewWithModelError()
        {
            // Arrange
            var flightVM = new FlightViewModel { Id = 1, AirlineId = 1 };
            _unitOfWorkMock.Setup(u => u.CompleteAsync()).ReturnsAsync(0);

            // Act
            var result = await _controller.Create(flightVM);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal(flightVM, viewResult.Model);
            Assert.False(_controller.ModelState.IsValid);
            Assert.Contains("Failed to save Flight.", _controller.ModelState[string.Empty].Errors[0].ErrorMessage);
        }

        [Fact]
        public async Task Create_Post_Exception_ShouldReturnViewWithModelError()
        {
            // Arrange
            var flightVM = new FlightViewModel { Id = 1, AirlineId = 1 };
            _flightRepoMock.Setup(r => r.AddAsync(It.IsAny<Flight>()))
                .ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await _controller.Create(flightVM);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal(flightVM, viewResult.Model);
            Assert.False(_controller.ModelState.IsValid);
            Assert.Contains("An error occurred while creating the Flight.", _controller.ModelState[string.Empty].Errors[0].ErrorMessage);
        }

        #endregion

        #region Edit (GET)

        [Fact]
        public async Task Edit_Get_InvalidId_ShouldReturnBadRequest()
        {
            // Act
            var result = await _controller.Edit(null);

            // Assert
            Assert.IsType<BadRequestResult>(result);
        }

        [Fact]
        public async Task Edit_Get_ValidIdNotFound_ShouldReturnNotFound()
        {
            // Arrange
            int id = 1;
            _cacheServiceMock.Setup(c => c.GetAsync<FlightViewModel>(It.IsAny<string>())).ReturnsAsync((FlightViewModel)null);
            _flightRepoMock.Setup(r => r.GetEntityWithSpecAsync(It.IsAny<ISpecification<Flight>>()))
                .ReturnsAsync((Flight)null);

            // Act
            var result = await _controller.Edit(id);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task Edit_Get_CacheHit_ShouldReturnViewWithFlightAndDropDowns()
        {
            // Arrange
            int id = 1;
            var flightVM = new FlightViewModel { Id = id, AirlineId = 2, DepartureAirportID = 3, ArrivalAirportID = 4 };
            _cacheServiceMock.Setup(c => c.GetAsync<FlightViewModel>($"flight:id:{id}")).ReturnsAsync(flightVM);

            var airlines = new List<Airline> { new Airline { Id = 2, Name = "A" } };
            var airports = new List<Airport> { new Airport { Id = 3, Name = "JFK" }, new Airport { Id = 4, Name = "LHR" } };
            var airplanes = new List<Airplane> { new Airplane { Id = 5, Model = "B737" } };

            _airlineRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(airlines);
            _airportRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(airports);
            _cacheServiceMock.Setup(c => c.GetOrSetAsync(
                    It.Is<string>(key => key == $"airplanes:flight:2"),
                    It.IsAny<Func<Task<List<Airplane>>>>(),
                    It.IsAny<TimeSpan>()))
                .ReturnsAsync(airplanes);

            // Act
            var result = await _controller.Edit(id);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<FlightViewModel>(viewResult.Model);
            Assert.Equal(id, model.Id);
            Assert.NotNull(_controller.ViewBag.Airlines);
            Assert.NotNull(_controller.ViewBag.Airports);
            Assert.NotNull(_controller.ViewBag.Airplanes);
        }

        [Fact]
        public async Task Edit_Get_CacheMiss_ShouldGetFromDbAndCache_AndReturnView()
        {
            // Arrange
            int id = 1;
            var flightEntity = new Flight
            {
                Id = id,
                AirlineId = 2,
                AirplaneId = 5,
                DepartureAirportID = 3,
                ArrivalAirportID = 4,
                Airline = new Airline(),
                Airplane = new Airplane(),
                DepartureAirport = new Airport(),
                ArrivalAirport = new Airport()
            };
            var expectedVM = (FlightViewModel)flightEntity;

            _cacheServiceMock.Setup(c => c.GetAsync<FlightViewModel>($"flight:id:{id}")).ReturnsAsync((FlightViewModel)null);
            _flightRepoMock.Setup(r => r.GetEntityWithSpecAsync(It.IsAny<ISpecification<Flight>>()))
                .ReturnsAsync(flightEntity);
            _cacheServiceMock.Setup(c => c.SetAsync($"flight:id:{id}", expectedVM, It.IsAny<TimeSpan>())).Returns(Task.CompletedTask);

            var airlines = new List<Airline> { new Airline { Id = 2, Name = "A" } };
            var airports = new List<Airport> { new Airport { Id = 3, Name = "JFK" }, new Airport { Id = 4, Name = "LHR" } };
            var airplanes = new List<Airplane> { new Airplane { Id = 5, Model = "B737" } };

            _airlineRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(airlines);
            _airportRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(airports);
            _cacheServiceMock.Setup(c => c.GetOrSetAsync(
                    It.Is<string>(key => key == $"airplanes:flight:2"),
                    It.IsAny<Func<Task<List<Airplane>>>>(),
                    It.IsAny<TimeSpan>()))
                .ReturnsAsync(airplanes);

            // Act
            var result = await _controller.Edit(id);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<FlightViewModel>(viewResult.Model);
            Assert.Equal(id, model.Id);
            _flightRepoMock.Verify(r => r.GetEntityWithSpecAsync(It.IsAny<ISpecification<Flight>>()), Times.Once);
            _cacheServiceMock.Verify(c => c.SetAsync($"flight:id:{id}", It.IsAny<FlightViewModel>(), It.IsAny<TimeSpan>()), Times.Once);
        }

        [Fact]
        public async Task Edit_Get_Exception_ShouldRedirectToIndexWithError()
        {
            // Arrange
            int id = 1;
            _cacheServiceMock.Setup(c => c.GetAsync<FlightViewModel>(It.IsAny<string>())).ThrowsAsync(new Exception("Cache error"));

            // Act
            var result = await _controller.Edit(id);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(nameof(FlightController.Index), redirectResult.ActionName);
            Assert.Equal("An error occurred while retrieving the Flight.", _controller.TempData["error"]);
        }

        #endregion

        #region Edit (POST)

        [Fact]
        public async Task Edit_Post_IdMismatch_ShouldReturnBadRequest()
        {
            // Arrange
            int routeId = 1;
            var flightVM = new FlightViewModel { Id = 2 };

            // Act
            var result = await _controller.Edit(routeId, flightVM);

            // Assert
            Assert.IsType<BadRequestResult>(result);
        }

        [Fact]
        public async Task Edit_Post_InvalidModel_ShouldReturnViewWithModelAndDropDowns()
        {
            // Arrange
            int id = 1;
            var flightVM = new FlightViewModel { Id = id, AirlineId = 2 };
            _controller.ModelState.AddModelError("FlightNumber", "Required");

            var airlines = new List<Airline> { new Airline { Id = 2, Name = "A" } };
            var airports = new List<Airport> { new Airport { Id = 3, Name = "JFK" } };
            var airplanes = new List<Airplane> { new Airplane { Id = 5, Model = "B737" } };

            _airlineRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(airlines);
            _airportRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(airports);
            _cacheServiceMock.Setup(c => c.GetOrSetAsync(
                    It.Is<string>(key => key == $"airplanes:flight:2"),
                    It.IsAny<Func<Task<List<Airplane>>>>(),
                    It.IsAny<TimeSpan>()))
                .ReturnsAsync(airplanes);

            // Act
            var result = await _controller.Edit(id, flightVM);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal(flightVM, viewResult.Model);
            Assert.NotNull(_controller.ViewBag.Airlines);
            Assert.NotNull(_controller.ViewBag.Airports);
        }

        [Fact]
        public async Task Edit_Post_ValidModel_ShouldUpdateAndRedirect_AndInvalidateCache_AndSendHubNotification()
        {
            // Arrange
            int id = 1;
            var flightVM = new FlightViewModel
            {
                Id = id,
                AirlineId = 2,
                AirplaneId = 3,
                DepartureAirportID = 4,
                ArrivalAirportID = 5,
                DepartureTime = DateTime.UtcNow,
                ArrivalTime = DateTime.UtcNow.AddHours(5),
                BasePrice = 300,
                Status = FlightStatus.Scheduled
            };
            var existingFlight = new Flight { Id = id }; 

            var flightRepoMock = new Mock<IGenericRepository<Flight>>();
            flightRepoMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(existingFlight);
            flightRepoMock.Setup(r => r.Update(It.IsAny<Flight>())).Verifiable();
            _unitOfWorkMock.Setup(u => u.Repository<Flight>()).Returns(flightRepoMock.Object);

            _unitOfWorkMock.Setup(u => u.CompleteAsync()).ReturnsAsync(1);

            _cacheServiceMock.Setup(c => c.RemoveAsync("flights:all")).Returns(Task.CompletedTask);
            _cacheServiceMock.Setup(c => c.RemoveAsync($"flight:id:{id}")).Returns(Task.CompletedTask);
            _cacheServiceMock.Setup(c => c.RemoveAsync($"flight:details:id:{id}")).Returns(Task.CompletedTask);
            _cacheServiceMock.Setup(c => c.RemoveByPatternAsync("airplanes:flight:*")).Returns(Task.CompletedTask);

            var clientsMock = new Mock<IHubClients>();
            var clientProxyMock = new Mock<IClientProxy>();
            clientsMock.Setup(c => c.Group(SD.Admin)).Returns(clientProxyMock.Object);
            _hubContextMock.Setup(h => h.Clients).Returns(clientsMock.Object);
            clientProxyMock.Setup(c => c.SendCoreAsync("ReceiveUpdate", It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _controller.Edit(id, flightVM);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(nameof(FlightController.Index), redirectResult.ActionName);
            Assert.Equal("Flight Updated Successfully", _controller.TempData["success"]);
            Assert.Equal(flightVM.AirlineId, existingFlight.AirlineId);
            Assert.Equal(flightVM.AirplaneId, existingFlight.AirplaneId);
            Assert.Equal(flightVM.DepartureAirportID, existingFlight.DepartureAirportID);
            Assert.Equal(flightVM.ArrivalAirportID, existingFlight.ArrivalAirportID);
            Assert.Equal(flightVM.BasePrice, existingFlight.BasePrice);
            flightRepoMock.Verify(r => r.Update(existingFlight), Times.Once);
            _unitOfWorkMock.Verify(u => u.CompleteAsync(), Times.Once);
            _cacheServiceMock.Verify(c => c.RemoveAsync("flights:all"), Times.Once);
            _cacheServiceMock.Verify(c => c.RemoveAsync($"flight:id:{id}"), Times.Once);
            _cacheServiceMock.Verify(c => c.RemoveAsync($"flight:details:id:{id}"), Times.Once);
            _cacheServiceMock.Verify(c => c.RemoveByPatternAsync("airplanes:flight:*"), Times.Once);
            clientProxyMock.Verify(c => c.SendCoreAsync("ReceiveUpdate", It.IsAny<object[]>(), It.IsAny<CancellationToken>()), Times.Once);
        }
        [Fact]
        public async Task Edit_Post_Exception_ShouldReturnViewWithModelError_InDevelopment()
        {
            // Arrange
            int id = 1;
            var flightVM = new FlightViewModel { Id = id };
            var existingFlight = new Flight { Id = id };
            var exception = new Exception("Update failed");

            var flightRepoMock = new Mock<IGenericRepository<Flight>>();
            flightRepoMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(existingFlight);
            _unitOfWorkMock.Setup(u => u.Repository<Flight>()).Returns(flightRepoMock.Object);
            _unitOfWorkMock.Setup(u => u.CompleteAsync()).ThrowsAsync(exception);

            _envMock.Setup(e => e.EnvironmentName).Returns("Development");

            // Act
            var result = await _controller.Edit(id, flightVM);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal(flightVM, viewResult.Model);
            Assert.False(_controller.ModelState.IsValid);
            Assert.Contains("Update failed", _controller.ModelState[string.Empty].Errors[0].ErrorMessage);
            _cacheServiceMock.Verify(c => c.RemoveAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task Edit_Post_Exception_ShouldReturnViewWithGenericError_InProduction()
        {
            // Arrange
            int id = 1;
            var flightVM = new FlightViewModel { Id = id };
            var existingFlight = new Flight { Id = id };
            var exception = new Exception("Update failed");

            var flightRepoMock = new Mock<IGenericRepository<Flight>>();
            flightRepoMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(existingFlight);
            _unitOfWorkMock.Setup(u => u.Repository<Flight>()).Returns(flightRepoMock.Object);
            _unitOfWorkMock.Setup(u => u.CompleteAsync()).ThrowsAsync(exception);

            _envMock.Setup(e => e.EnvironmentName).Returns("Production");

            // Act
            var result = await _controller.Edit(id, flightVM);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal(flightVM, viewResult.Model);
            Assert.False(_controller.ModelState.IsValid);
            Assert.Contains("An Error Has Occurred during Updating the Flight", _controller.ModelState[string.Empty].Errors[0].ErrorMessage);
            _cacheServiceMock.Verify(c => c.RemoveAsync(It.IsAny<string>()), Times.Never);
        }

        #endregion

        #region Delete

        [Fact]
        public async Task Delete_InvalidId_ShouldReturnJsonError()
        {
            // Act
            var result = await _controller.Delete(null);

            // Assert
            var jsonResult = Assert.IsType<JsonResult>(result);
            var jsonString = JsonSerializer.Serialize(jsonResult.Value);
            using var jsonDoc = JsonDocument.Parse(jsonString);
            var root = jsonDoc.RootElement;

            Assert.False(root.GetProperty("success").GetBoolean());
            Assert.Equal("Invalid ID", root.GetProperty("message").GetString());
        }

        [Fact]
        public async Task Delete_ValidIdNotFound_ShouldReturnJsonNotFound()
        {
            // Arrange
            int id = 1;
            _flightRepoMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync((Flight)null);

            // Act
            var result = await _controller.Delete(id);

            // Assert
            var jsonResult = Assert.IsType<JsonResult>(result);
            var jsonString = JsonSerializer.Serialize(jsonResult.Value);
            using var jsonDoc = JsonDocument.Parse(jsonString);
            var root = jsonDoc.RootElement;

            Assert.False(root.GetProperty("success").GetBoolean());
            Assert.Equal("Flight not found", root.GetProperty("message").GetString());
        }

        [Fact]
        public async Task Delete_Valid_ShouldDeleteAndReturnSuccess_AndInvalidateCache_AndSendHubNotification()
        {
            // Arrange
            int id = 1;
            var flightEntity = new Flight { Id = id };
            _flightRepoMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(flightEntity);
            _unitOfWorkMock.Setup(u => u.CompleteAsync()).ReturnsAsync(1);

            _cacheServiceMock.Setup(c => c.RemoveAsync("flights:all")).Returns(Task.CompletedTask);
            _cacheServiceMock.Setup(c => c.RemoveAsync($"flight:id:{id}")).Returns(Task.CompletedTask);
            _cacheServiceMock.Setup(c => c.RemoveAsync($"flight:details:id:{id}")).Returns(Task.CompletedTask);
            _cacheServiceMock.Setup(c => c.RemoveByPatternAsync("airplanes:flight:*")).Returns(Task.CompletedTask);

            var clientsMock = new Mock<IHubClients>();
            var clientProxyMock = new Mock<IClientProxy>();
            clientsMock.Setup(c => c.Group(SD.Admin)).Returns(clientProxyMock.Object);
            _hubContextMock.Setup(h => h.Clients).Returns(clientsMock.Object);
            clientProxyMock.Setup(c => c.SendCoreAsync("ReceiveUpdate", It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _controller.Delete(id);

            // Assert
            var jsonResult = Assert.IsType<JsonResult>(result);
            var jsonString = JsonSerializer.Serialize(jsonResult.Value);
            using var jsonDoc = JsonDocument.Parse(jsonString);
            var root = jsonDoc.RootElement;

            Assert.True(root.GetProperty("success").GetBoolean());
            Assert.Equal("Delete Successful", root.GetProperty("message").GetString());

            _flightRepoMock.Verify(r => r.Delete(flightEntity), Times.Once);
            _unitOfWorkMock.Verify(u => u.CompleteAsync(), Times.Once);
            _cacheServiceMock.Verify(c => c.RemoveAsync("flights:all"), Times.Once);
            _cacheServiceMock.Verify(c => c.RemoveAsync($"flight:id:{id}"), Times.Once);
            _cacheServiceMock.Verify(c => c.RemoveAsync($"flight:details:id:{id}"), Times.Once);
            _cacheServiceMock.Verify(c => c.RemoveByPatternAsync("airplanes:flight:*"), Times.Once);
            clientProxyMock.Verify(c => c.SendCoreAsync("ReceiveUpdate", It.IsAny<object[]>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Delete_Exception_ShouldReturnJsonError()
        {
            // Arrange
            int id = 1;
            var flightEntity = new Flight { Id = id };
            _flightRepoMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(flightEntity);
            _unitOfWorkMock.Setup(u => u.CompleteAsync()).ThrowsAsync(new Exception("Delete failed"));

            // Act
            var result = await _controller.Delete(id);

            // Assert
            var jsonResult = Assert.IsType<JsonResult>(result);
            var jsonString = JsonSerializer.Serialize(jsonResult.Value);
            using var jsonDoc = JsonDocument.Parse(jsonString);
            var root = jsonDoc.RootElement;

            Assert.False(root.GetProperty("success").GetBoolean());
            Assert.Equal("Error While deleting", root.GetProperty("message").GetString());
        }

        #endregion

        #region Details

        [Fact]
        public async Task Details_InvalidId_ShouldReturnBadRequest()
        {
            // Act
            var result = await _controller.Details(null);

            // Assert
            Assert.IsType<BadRequestResult>(result);
        }

        [Fact]
        public async Task Details_ValidIdNotFound_ShouldReturnNotFound()
        {
            // Arrange
            int id = 1;
            _cacheServiceMock.Setup(c => c.GetAsync<FlightViewModel>(It.IsAny<string>())).ReturnsAsync((FlightViewModel)null);
            _flightRepoMock.Setup(r => r.GetEntityWithSpecAsync(It.IsAny<ISpecification<Flight>>()))
                .ReturnsAsync((Flight)null);

            // Act
            var result = await _controller.Details(id);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task Details_CacheHit_ShouldReturnViewWithFlight()
        {
            // Arrange
            int id = 1;
            var flightVM = new FlightViewModel { Id = id };
            _cacheServiceMock.Setup(c => c.GetAsync<FlightViewModel>($"flight:details:id:{id}")).ReturnsAsync(flightVM);

            // Act
            var result = await _controller.Details(id);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<FlightViewModel>(viewResult.Model);
            Assert.Equal(id, model.Id);
            _flightRepoMock.Verify(r => r.GetEntityWithSpecAsync(It.IsAny<ISpecification<Flight>>()), Times.Never);
        }

        [Fact]
        public async Task Details_CacheMiss_ShouldGetFromDbAndCacheBothKeys_AndReturnView()
        {
            // Arrange
            int id = 1;
            var flightEntity = new Flight
            {
                Id = id,
                Airline = new Airline(),
                Airplane = new Airplane(),
                DepartureAirport = new Airport(),
                ArrivalAirport = new Airport()
            };
            var expectedVM = (FlightViewModel)flightEntity;

            _cacheServiceMock.Setup(c => c.GetAsync<FlightViewModel>($"flight:details:id:{id}")).ReturnsAsync((FlightViewModel)null);
            _flightRepoMock.Setup(r => r.GetEntityWithSpecAsync(It.IsAny<ISpecification<Flight>>()))
                .ReturnsAsync(flightEntity);
            _cacheServiceMock.Setup(c => c.SetAsync($"flight:details:id:{id}", expectedVM, It.IsAny<TimeSpan>())).Returns(Task.CompletedTask);
            _cacheServiceMock.Setup(c => c.SetAsync($"flight:id:{id}", expectedVM, It.IsAny<TimeSpan>())).Returns(Task.CompletedTask);

            // Act
            var result = await _controller.Details(id);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<FlightViewModel>(viewResult.Model);
            Assert.Equal(id, model.Id);
            _flightRepoMock.Verify(r => r.GetEntityWithSpecAsync(It.IsAny<ISpecification<Flight>>()), Times.Once);
            _cacheServiceMock.Verify(c => c.SetAsync($"flight:details:id:{id}", It.IsAny<FlightViewModel>(), It.IsAny<TimeSpan>()), Times.Once);
            _cacheServiceMock.Verify(c => c.SetAsync($"flight:id:{id}", It.IsAny<FlightViewModel>(), It.IsAny<TimeSpan>()), Times.Once);
        }

        [Fact]
        public async Task Details_Exception_ShouldRedirectToIndexWithError()
        {
            // Arrange
            int id = 1;
            _cacheServiceMock.Setup(c => c.GetAsync<FlightViewModel>(It.IsAny<string>())).ThrowsAsync(new Exception("Cache error"));

            // Act
            var result = await _controller.Details(id);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(nameof(FlightController.Index), redirectResult.ActionName);
            Assert.Equal("An error occurred while retrieving the Flight details.", _controller.TempData["error"]);
        }

        #endregion

        #region Search

        [Fact]
        public async Task Search_ShouldReturnViewWithFilteredFlights_WhenKeywordProvided()
        {
            // Arrange
            string keyword = "test";
            var flights = new List<Flight>
            {
                 new Flight
                 {
                     Id = 1,
                     Airline = new Airline { Name = "test airlines" },
                     Airplane = new Airplane { Model = "B737" },
                     DepartureAirport = new Airport { Name = "test airport" },
                     ArrivalAirport = new Airport { Name = "destination airport" }
                 },
                 new Flight
                 {
                     Id = 2,
                     Airline = new Airline { Name = "test airlines" },
                     Airplane = new Airplane { Model = "A320" },
                     DepartureAirport = new Airport { Name = "test airport" },
                     ArrivalAirport = new Airport { Name = "destination airport" }
                 }
            };

            _flightRepoMock.Setup(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<Flight>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(flights);

            // Act
            var result = await _controller.Search(keyword, null);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<IEnumerable<FlightViewModel>>(viewResult.Model);
            Assert.Equal(2, model.Count());
            Assert.Equal(keyword, _controller.ViewBag.keyword);
        }

        [Fact]
        public async Task Search_ShouldReturnViewWithFilteredFlights_WhenDateProvided()
        {
            // Arrange
            DateTime date = DateTime.Today;
            var flights = new List<Flight>
            {
                new Flight
                {
                    Id = 1,
                    Airline = new Airline { Name = "EgyptAir" },
                    Airplane = new Airplane { Model = "B737" },
                    DepartureAirport = new Airport { Name = "Cairo" },
                    ArrivalAirport = new Airport { Name = "Dubai" },
                    DepartureTime = date
                }
            };

            _flightRepoMock.Setup(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<Flight>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(flights);

            // Act
            var result = await _controller.Search(null, date);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<IEnumerable<FlightViewModel>>(viewResult.Model);
            Assert.Single(model);
            Assert.Equal(date, _controller.ViewBag.date);
        }

        [Fact]
        public async Task Search_Exception_ShouldReturnEmptyViewWithModelError()
        {
            // Arrange
            _flightRepoMock.Setup(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<Flight>>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await _controller.Search("test", null);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<IEnumerable<FlightViewModel>>(viewResult.Model);
            Assert.Empty(model);
            Assert.False(_controller.ModelState.IsValid);
            Assert.Contains("An error occurred while searching flights.", _controller.ModelState[string.Empty].Errors[0].ErrorMessage);
        }

        #endregion

        #region GetAirplanesByAirlineId

        [Fact]
        public async Task GetAirplanesByAirlineId_ShouldReturnJsonWithAirplanes_WhenCacheHit()
        {
            // Arrange
            int airlineId = 1;
            var airplanes = new List<object> { new { Id = 1, Model = "B737" }, new { Id = 2, Model = "A320" } };

            _cacheServiceMock
                .Setup(c => c.GetOrSetAsync(
                    It.IsAny<string>(),
                    It.IsAny<Func<Task<List<object>>>>(),
                    It.IsAny<TimeSpan>()))
                    .ReturnsAsync(airplanes);

            // Act
            var result = await _controller.GetAirplanesByAirlineId(airlineId);

            // Assert
            var jsonResult = Assert.IsType<JsonResult>(result);
            var enumerable = jsonResult.Value as IEnumerable<object>;
            Assert.NotNull(enumerable);
            Assert.Equal(2, enumerable.Count());
        }

        [Fact]
        public async Task GetAirplanesByAirlineId_ShouldReturnJsonWithAirplanes_WhenCacheMiss()
        {
            // Arrange
            int airlineId = 1;
            var airplaneEntities = new List<Airplane>
            {
                new Airplane { Id = 1, Model = "B737" },
                new Airplane { Id = 2, Model = "A320" }
            };
            var expectedResult = airplaneEntities.Select(e => new { Id = e.Id, Model = e.Model }).ToList();

            _cacheServiceMock
               .Setup(c => c.GetOrSetAsync(
                   It.Is<string>(key => key == $"airplanes:flight:{airlineId}"),
                   It.IsAny<Func<Task<List<object>>>>(),
                   It.IsAny<TimeSpan>()))
               .Returns((string key, Func<Task<List<object>>> factory, TimeSpan expiry) => factory());


            _airplaneRepoMock.Setup(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<Airplane>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(airplaneEntities);

            // Act
            var result = await _controller.GetAirplanesByAirlineId(airlineId);

            // Assert
            var jsonResult = Assert.IsType<JsonResult>(result);
            var jsonString = JsonSerializer.Serialize(jsonResult.Value);
            using var jsonDoc = JsonDocument.Parse(jsonString);
            var root = jsonDoc.RootElement;

            Assert.Equal(2, root.GetArrayLength());
            _airplaneRepoMock.Verify(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<Airplane>>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetAirplanesByAirlineId_Exception_ShouldReturnEmptyJson()
        {
            // Arrange
            int airlineId = 1;
            _cacheServiceMock.Setup(c => c.GetOrSetAsync(
                    It.IsAny<string>(),
                    It.IsAny<Func<Task<List<object>>>>(),
                    It.IsAny<TimeSpan>()))
                .ThrowsAsync(new Exception("Cache error"));

            // Act
            var result = await _controller.GetAirplanesByAirlineId(airlineId);

            // Assert
            var jsonResult = Assert.IsType<JsonResult>(result);
            var jsonString = JsonSerializer.Serialize(jsonResult.Value);
            using var jsonDoc = JsonDocument.Parse(jsonString);
            var root = jsonDoc.RootElement;

            Assert.Equal(0, root.GetArrayLength());
        }

        #endregion
    }
}