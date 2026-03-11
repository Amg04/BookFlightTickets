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
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using System.Linq;
namespace BookFlightTickets.ControllerTests.Admin
{
    public class AirplaneControllerTests
    {
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly Mock<IGenericRepository<Airplane>> _airplaneRepositoryMock;
        private readonly Mock<IGenericRepository<Airline>> _airlineRepositoryMock;
        private readonly Mock<IRedisCacheService> _cacheServiceMock;
        private readonly Mock<IWebHostEnvironment> _envMock;
        private readonly Mock<ILogger<AirplaneController>> _loggerMock;
        private readonly Mock<IHubContext<DashboardHub>> _hubContextMock;
        private readonly AirplaneController _controller;
        private readonly ITempDataDictionary _tempData;

        public AirplaneControllerTests()
        {
            _unitOfWorkMock = new Mock<IUnitOfWork>();
            _airplaneRepositoryMock = new Mock<IGenericRepository<Airplane>>();
            _airlineRepositoryMock = new Mock<IGenericRepository<Airline>>();
            _cacheServiceMock = new Mock<IRedisCacheService>();
            _envMock = new Mock<IWebHostEnvironment>();
            _loggerMock = new Mock<ILogger<AirplaneController>>();
            _hubContextMock = new Mock<IHubContext<DashboardHub>>();

            // Setup UnitOfWork to return the mocked repositories
            _unitOfWorkMock.Setup(u => u.Repository<Airplane>()).Returns(_airplaneRepositoryMock.Object);
            _unitOfWorkMock.Setup(u => u.Repository<Airline>()).Returns(_airlineRepositoryMock.Object);

            // Create controller instance
            _controller = new AirplaneController(
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
        public async Task Index_ShouldReturnViewWithAirplanes_WhenCacheHit()
        {
            // Arrange
            var airplanes = new List<AirplaneViewModel>
            {
                new AirplaneViewModel { Id = 1, Model = "Boeing 737", SeatCapacity = 180, AirlineId = 1 },
                new AirplaneViewModel { Id = 2, Model = "Airbus A320", SeatCapacity = 200, AirlineId = 2 }
            };

            _cacheServiceMock.Setup(c => c.GetOrSetAsync(
                    It.Is<string>(key => key == "airplanes:all"),
                    It.IsAny<Func<Task<List<AirplaneViewModel>>>>(),
                    It.IsAny<TimeSpan>()))
                .ReturnsAsync(airplanes);

            // Act
            var result = await _controller.Index();

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<IEnumerable<AirplaneViewModel>>(viewResult.Model);
            Assert.Equal(2, model.Count());
            _cacheServiceMock.Verify(c => c.GetOrSetAsync("airplanes:all", It.IsAny<Func<Task<List<AirplaneViewModel>>>>(), It.IsAny<TimeSpan>()), Times.Once);
        }

        [Fact]
        public async Task Index_ShouldReturnViewWithAirplanes_WhenCacheMiss()
        {
            // Arrange
            var airplaneEntities = new List<Airplane>
            {
                new Airplane { Id = 1, Model = "Boeing 737", SeatCapacity = 180, AirlineId = 1, Airline = new Airline { Id = 1, Name =      "Airline1" } },
                new Airplane { Id = 2, Model = "Airbus A320", SeatCapacity = 200, AirlineId = 2, Airline = new Airline { Id = 2, Name =         "Airline2" } }
            };

            _airplaneRepositoryMock
            .Setup(r => r.GetAllWithSpecAsync(It.IsAny<BaseSpecification<Airplane>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(airplaneEntities);

            _cacheServiceMock
                .Setup(c => c.GetOrSetAsync(
                    It.Is<string>(key => key == "airplanes:all"),
                    It.IsAny<Func<Task<List<AirplaneViewModel>>>>(),   
                    It.IsAny<TimeSpan>()))
                .Returns((string key, Func<Task<List<AirplaneViewModel>>> factory, TimeSpan expiry) => factory());  

            // Act
            var result = await _controller.Index();

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<IEnumerable<AirplaneViewModel>>(viewResult.Model);
            Assert.Equal(2, model.Count());
            _airplaneRepositoryMock.Verify(
                 r => r.GetAllWithSpecAsync(It.IsAny<BaseSpecification<Airplane>>(), It.IsAny<CancellationToken>()),
                 Times.Once);
        }

        [Fact]
        public async Task Index_ShouldReturnViewWithInfoMessage_WhenNoAirplanes()
        {
            // Arrange
            _cacheServiceMock.Setup(c => c.GetOrSetAsync(
                    It.IsAny<string>(),
                    It.IsAny<Func<Task<List<AirplaneViewModel>>>>(),
                    It.IsAny<TimeSpan>()))
                .ReturnsAsync(new List<AirplaneViewModel>());

            // Act
            var result = await _controller.Index();

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<IEnumerable<AirplaneViewModel>>(viewResult.Model);
            Assert.Empty(model);
            Assert.Equal("No airplanes available.", _controller.ViewBag.InfoMessage);
        }

        [Fact]
        public async Task Index_ShouldReturnViewWithError_WhenExceptionOccurs()
        {
            // Arrange
            _cacheServiceMock.Setup(c => c.GetOrSetAsync(
                    It.IsAny<string>(),
                    It.IsAny<Func<Task<List<AirplaneViewModel>>>>(),
                    It.IsAny<TimeSpan>()))
                .ThrowsAsync(new Exception("Test exception"));

            // Act
            var result = await _controller.Index();

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<IEnumerable<AirplaneViewModel>>(viewResult.Model);
            Assert.Empty(model);
            Assert.Equal("An error occurred while retrieving airplanes.", _controller.ViewBag.ErrorMessage);
        }

        #endregion

        #region Create (GET)

        [Fact]
        public async Task Create_Get_ShouldReturnViewWithAirlinesDropdown()
        {
            // Arrange
            var airlines = new List<Airline>
            {
                new Airline { Id = 1, Name = "Airline1" },
                new Airline { Id = 2, Name = "Airline2" }
            };
            var selectListItems = airlines.Select(a => new SelectListItem { Text = a.Name, Value = a.Id.ToString() }).ToList();

            _cacheServiceMock.Setup(c => c.GetOrSetAsync(
                    It.Is<string>(key => key == "airlines:dropdown"),
                    It.IsAny<Func<Task<List<SelectListItem>>>>(),
                    It.IsAny<TimeSpan>()))
                .ReturnsAsync(selectListItems);

            // Act
            var result = await _controller.Create();

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal(selectListItems, _controller.ViewBag.Airlines);
        }

        [Fact]
        public async Task Create_Get_WhenException_ShouldRedirectToIndexWithError()
        {
            // Arrange
            _cacheServiceMock.Setup(c => c.GetOrSetAsync(
                    It.IsAny<string>(),
                    It.IsAny<Func<Task<List<SelectListItem>>>>(),
                    It.IsAny<TimeSpan>()))
                .ThrowsAsync(new Exception("Cache error"));

            // Act
            var result = await _controller.Create();

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(nameof(AirplaneController.Index), redirectResult.ActionName);
            Assert.Equal("An error occurred while loading airlines.", _controller.TempData["error"]);
        }

        #endregion

        #region Create (POST)

        [Fact]
        public async Task Create_Post_InvalidModel_ShouldReturnViewWithModelAndDropdown()
        {
            // Arrange
            var airplaneVM = new AirplaneViewModel { Model = "Test", SeatCapacity = 100, AirlineId = 1 };
            _controller.ModelState.AddModelError("Model", "Required");

            var airlines = new List<Airline>
            {
                new Airline { Id = 1, Name = "Airline1" }
            };
            var selectListItems = airlines.Select(a => new SelectListItem { Text = a.Name, Value = a.Id.ToString() }).ToList();

            _cacheServiceMock.Setup(c => c.GetOrSetAsync(
                    It.Is<string>(key => key == "airlines:dropdown"),
                    It.IsAny<Func<Task<List<SelectListItem>>>>(),
                    It.IsAny<TimeSpan>()))
                .ReturnsAsync(selectListItems);

            // Act
            var result = await _controller.Create(airplaneVM);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal(airplaneVM, viewResult.Model);
            Assert.Equal(selectListItems, _controller.ViewBag.Airlines);
            _airplaneRepositoryMock.Verify(r => r.AddAsync(It.IsAny<Airplane>()), Times.Never);
        }

        [Fact]
        public async Task Create_Post_ValidModel_ShouldAddAndRedirect_AndInvalidateCache_AndSendHubNotification()
        {
            // Arrange
            var airplaneVM = new AirplaneViewModel { Id = 0, Model = "New Airplane", SeatCapacity = 150, AirlineId = 1 };
            var airplaneEntity = (Airplane)airplaneVM;

            _airplaneRepositoryMock.Setup(r => r.AddAsync(It.IsAny<Airplane>()))
                .Callback<Airplane>(a => a.Id = 1)
                .Returns(Task.CompletedTask);

            _unitOfWorkMock.Setup(u => u.CompleteAsync()).ReturnsAsync(1);

            _cacheServiceMock.Setup(c => c.RemoveAsync("airplanes:all")).Returns(Task.CompletedTask);
            _cacheServiceMock.Setup(c => c.RemoveByPatternAsync("airplane:id:*")).Returns(Task.CompletedTask);
            _cacheServiceMock.Setup(c => c.RemoveByPatternAsync("airplane:with-airline:id:*")).Returns(Task.CompletedTask);

            var clientsMock = new Mock<IHubClients>();
            var clientProxyMock = new Mock<IClientProxy>();
            clientsMock.Setup(c => c.Group(SD.Admin)).Returns(clientProxyMock.Object);
            _hubContextMock.Setup(h => h.Clients).Returns(clientsMock.Object);
            clientProxyMock.Setup(c => c.SendCoreAsync("ReceiveUpdate", It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _controller.Create(airplaneVM);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(nameof(AirplaneController.Index), redirectResult.ActionName);
            Assert.Equal("Airplane has been Added Successfully", _controller.TempData["success"]);
            _airplaneRepositoryMock.Verify(r => r.AddAsync(It.Is<Airplane>(a => a.Model == airplaneVM.Model && a.SeatCapacity == airplaneVM.SeatCapacity && a.AirlineId == airplaneVM.AirlineId)), Times.Once);
            _unitOfWorkMock.Verify(u => u.CompleteAsync(), Times.Once);
            _cacheServiceMock.Verify(c => c.RemoveAsync("airplanes:all"), Times.Once);
            _cacheServiceMock.Verify(c => c.RemoveByPatternAsync("airplane:id:*"), Times.Once);
            _cacheServiceMock.Verify(c => c.RemoveByPatternAsync("airplane:with-airline:id:*"), Times.Once);
            clientProxyMock.Verify(c => c.SendCoreAsync("ReceiveUpdate", It.IsAny<object[]>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Create_Post_WhenSaveFails_ShouldReturnViewWithModelErrorAndDropdown()
        {
            // Arrange
            var airplaneVM = new AirplaneViewModel { Model = "Test", SeatCapacity = 100, AirlineId = 1 };
            _unitOfWorkMock.Setup(u => u.CompleteAsync()).ReturnsAsync(0);

            var airlines = new List<Airline>
            {
                new Airline { Id = 1, Name = "Airline1" }
            };
            var selectListItems = airlines.Select(a => new SelectListItem { Text = a.Name, Value = a.Id.ToString() }).ToList();

            _cacheServiceMock.Setup(c => c.GetOrSetAsync(
                    It.Is<string>(key => key == "airlines:dropdown"),
                    It.IsAny<Func<Task<List<SelectListItem>>>>(),
                    It.IsAny<TimeSpan>()))
                .ReturnsAsync(selectListItems);

            // Act
            var result = await _controller.Create(airplaneVM);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal(airplaneVM, viewResult.Model);
            Assert.False(_controller.ModelState.IsValid);
            Assert.Contains("Failed to save Airplane.", _controller.ModelState[string.Empty].Errors[0].ErrorMessage);
            Assert.Equal(selectListItems, _controller.ViewBag.Airlines);
        }

        [Fact]
        public async Task Create_Post_Exception_ShouldReturnViewWithModelErrorAndDropdown()
        {
            // Arrange
            var airplaneVM = new AirplaneViewModel { Model = "Test", SeatCapacity = 100, AirlineId = 1 };
            _airplaneRepositoryMock.Setup(r => r.AddAsync(It.IsAny<Airplane>()))
                .ThrowsAsync(new Exception("Database error"));

            var airlines = new List<Airline>
            {
                new Airline { Id = 1, Name = "Airline1" }
            };
            var selectListItems = airlines.Select(a => new SelectListItem { Text = a.Name, Value = a.Id.ToString() }).ToList();

            _cacheServiceMock.Setup(c => c.GetOrSetAsync(
                    It.Is<string>(key => key == "airlines:dropdown"),
                    It.IsAny<Func<Task<List<SelectListItem>>>>(),
                    It.IsAny<TimeSpan>()))
                .ReturnsAsync(selectListItems);

            // Act
            var result = await _controller.Create(airplaneVM);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal(airplaneVM, viewResult.Model);
            Assert.False(_controller.ModelState.IsValid);
            Assert.Contains("An error occurred while creating the Airplane.", _controller.ModelState[string.Empty].Errors[0].ErrorMessage);
            Assert.Equal(selectListItems, _controller.ViewBag.Airlines);
        }

        [Fact]
        public async Task Create_Post_HubNotificationException_ShouldLogErrorButNotFail()
        {
            // Arrange
            var airplaneVM = new AirplaneViewModel { Id = 0, Model = "New Airplane", SeatCapacity = 150, AirlineId = 1 };
            var airplaneEntity = (Airplane)airplaneVM;

            _airplaneRepositoryMock.Setup(r => r.AddAsync(It.IsAny<Airplane>()))
                .Callback<Airplane>(a => a.Id = 1)
                .Returns(Task.CompletedTask);

            _unitOfWorkMock.Setup(u => u.CompleteAsync()).ReturnsAsync(1);
            _cacheServiceMock.Setup(c => c.RemoveAsync("airplanes:all")).Returns(Task.CompletedTask);

            var clientsMock = new Mock<IHubClients>();
            var clientProxyMock = new Mock<IClientProxy>();
            clientsMock.Setup(c => c.Group(SD.Admin)).Returns(clientProxyMock.Object);
            _hubContextMock.Setup(h => h.Clients).Returns(clientsMock.Object);
            clientProxyMock.Setup(c => c.SendCoreAsync("ReceiveUpdate", It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("SignalR error"));

            // Act
            var result = await _controller.Create(airplaneVM);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(nameof(AirplaneController.Index), redirectResult.ActionName);
            Assert.Equal("Airplane has been Added Successfully", _controller.TempData["success"]);
            _airplaneRepositoryMock.Verify(r => r.AddAsync(It.IsAny<Airplane>()), Times.Once);
            _unitOfWorkMock.Verify(u => u.CompleteAsync(), Times.Once);
            clientProxyMock.Verify(c => c.SendCoreAsync("ReceiveUpdate", It.IsAny<object[]>(), It.IsAny<CancellationToken>()), Times.Once);
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
            _cacheServiceMock.Setup(c => c.GetAsync<AirplaneViewModel>(It.IsAny<string>())).ReturnsAsync((AirplaneViewModel)null);
            _airplaneRepositoryMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync((Airplane)null);

            // Act
            var result = await _controller.Edit(id);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task Edit_Get_CacheHit_ShouldReturnViewWithAirplaneAndDropdown()
        {
            // Arrange
            int id = 1;
            var airplaneVM = new AirplaneViewModel { Id = id, Model = "Cached", SeatCapacity = 100, AirlineId = 1 };
            _cacheServiceMock.Setup(c => c.GetAsync<AirplaneViewModel>($"airplane:with-airline:id:{id}")).ReturnsAsync(airplaneVM);

            var airlines = new List<Airline>
            {
                new Airline { Id = 1, Name = "Airline1" }
            };
            var selectListItems = airlines.Select(a => new SelectListItem { Text = a.Name, Value = a.Id.ToString() }).ToList();

            _cacheServiceMock.Setup(c => c.GetOrSetAsync(
                    It.Is<string>(key => key == "airlines:dropdown"),
                    It.IsAny<Func<Task<List<SelectListItem>>>>(),
                    It.IsAny<TimeSpan>()))
                .ReturnsAsync(selectListItems);

            // Act
            var result = await _controller.Edit(id);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<AirplaneViewModel>(viewResult.Model);
            Assert.Equal(id, model.Id);
            Assert.Equal(selectListItems, _controller.ViewBag.Airlines);
            _airplaneRepositoryMock.Verify(r => r.GetByIdAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task Edit_Get_CacheMiss_ShouldGetFromDbAndCache_AndReturnView()
        {
            // Arrange
            int id = 1;
            var airplaneEntity = new Airplane { Id = id, Model = "FromDb", SeatCapacity = 150, AirlineId = 1 };
            var airplaneWithAirlineEntity = new Airplane { Id = id, Model = "FromDb", SeatCapacity = 150, AirlineId = 1, Airline = new Airline { Id = 1, Name = "Airline1" } };
            var expectedVM = (AirplaneViewModel)airplaneWithAirlineEntity;

            _cacheServiceMock.Setup(c => c.GetAsync<AirplaneViewModel>($"airplane:with-airline:id:{id}")).ReturnsAsync((AirplaneViewModel)null);
            _airplaneRepositoryMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(airplaneEntity);
            _airplaneRepositoryMock.Setup(r => r.GetEntityWithSpecAsync(It.IsAny<BaseSpecification<Airplane>>()))
                .ReturnsAsync(airplaneWithAirlineEntity);

            var airlines = new List<Airline>
            {
                new Airline { Id = 1, Name = "Airline1" }
            };
            var selectListItems = airlines.Select(a => new SelectListItem { Text = a.Name, Value = a.Id.ToString() }).ToList();

            _cacheServiceMock.Setup(c => c.GetOrSetAsync(
                    It.Is<string>(key => key == "airlines:dropdown"),
                    It.IsAny<Func<Task<List<SelectListItem>>>>(),
                    It.IsAny<TimeSpan>()))
                .ReturnsAsync(selectListItems);

            _cacheServiceMock.Setup(c => c.SetAsync($"airplane:with-airline:id:{id}", expectedVM, It.IsAny<TimeSpan>())).Returns(Task.CompletedTask);

            // Act
            var result = await _controller.Edit(id);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<AirplaneViewModel>(viewResult.Model);
            Assert.Equal(id, model.Id);
            _airplaneRepositoryMock.Verify(r => r.GetByIdAsync(id), Times.Once);
            _airplaneRepositoryMock.Verify(r => r.GetEntityWithSpecAsync(It.IsAny<BaseSpecification<Airplane>>()), Times.Once);
            _cacheServiceMock.Verify(c => c.SetAsync(
               $"airplane:with-airline:id:{id}",
               It.Is<AirplaneViewModel>(vm => vm.Id == id && vm.Model == "FromDb" && vm.SeatCapacity == 150 && vm.AirlineId == 1),
               It.IsAny<TimeSpan>()),
               Times.Once);
            Assert.Equal(selectListItems, _controller.ViewBag.Airlines);
        }

        [Fact]
        public async Task Edit_Get_Exception_ShouldRedirectToIndexWithError()
        {
            // Arrange
            int id = 1;
            _cacheServiceMock.Setup(c => c.GetAsync<AirplaneViewModel>(It.IsAny<string>())).ThrowsAsync(new Exception("Cache error"));

            // Act
            var result = await _controller.Edit(id);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(nameof(AirplaneController.Index), redirectResult.ActionName);
            Assert.Equal("An error occurred while retrieving the Airplane.", _controller.TempData["error"]);
        }

        #endregion

        #region Edit (POST)

        [Fact]
        public async Task Edit_Post_IdMismatch_ShouldReturnBadRequest()
        {
            // Arrange
            int routeId = 1;
            var airplaneVM = new AirplaneViewModel { Id = 2, Model = "Test", SeatCapacity = 100, AirlineId = 1 };

            // Act
            var result = await _controller.Edit(routeId, airplaneVM);

            // Assert
            Assert.IsType<BadRequestResult>(result);
        }

        [Fact]
        public async Task Edit_Post_InvalidModel_ShouldReturnViewWithModelAndDropdown()
        {
            // Arrange
            int id = 1;
            var airplaneVM = new AirplaneViewModel { Id = id, Model = "Test", SeatCapacity = 100, AirlineId = 1 };
            _controller.ModelState.AddModelError("Model", "Required");

            var airlines = new List<Airline>
            {
                new Airline { Id = 1, Name = "Airline1" }
            };
            var selectListItems = airlines.Select(a => new SelectListItem { Text = a.Name, Value = a.Id.ToString() }).ToList();

            _cacheServiceMock.Setup(c => c.GetOrSetAsync(
                    It.Is<string>(key => key == "airlines:dropdown"),
                    It.IsAny<Func<Task<List<SelectListItem>>>>(),
                    It.IsAny<TimeSpan>()))
                .ReturnsAsync(selectListItems);

            // Act
            var result = await _controller.Edit(id, airplaneVM);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal(airplaneVM, viewResult.Model);
            Assert.Equal(selectListItems, _controller.ViewBag.Airlines);
            _unitOfWorkMock.Verify(u => u.CompleteAsync(), Times.Never);
        }

        [Fact]
        public async Task Edit_Post_ValidModel_ShouldUpdateAndRedirect_AndInvalidateCache()
        {
            // Arrange
            int id = 1;
            var airplaneVM = new AirplaneViewModel { Id = id, Model = "Updated", SeatCapacity = 200, AirlineId = 2 };
            var airplaneEntity = (Airplane)airplaneVM;

            var existingAirplane = new Airplane { Id = id, Model = "Old", SeatCapacity = 150, AirlineId = 1 };
            _airplaneRepositoryMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(existingAirplane);

            _unitOfWorkMock.Setup(u => u.CompleteAsync()).ReturnsAsync(1);

            _cacheServiceMock.Setup(c => c.RemoveAsync("airplanes:all")).Returns(Task.CompletedTask);
            _cacheServiceMock.Setup(c => c.RemoveAsync($"airplane:id:{id}")).Returns(Task.CompletedTask);
            _cacheServiceMock.Setup(c => c.RemoveAsync($"airplane:with-airline:id:{id}")).Returns(Task.CompletedTask);

            // Act
            var result = await _controller.Edit(id, airplaneVM);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(nameof(AirplaneController.Index), redirectResult.ActionName);
            Assert.Equal("Airplane Updated Successfully", _controller.TempData["success"]);
            Assert.Equal("Updated", existingAirplane.Model);
            Assert.Equal(200, existingAirplane.SeatCapacity);
            Assert.Equal(2, existingAirplane.AirlineId);
            _airplaneRepositoryMock.Verify(r => r.Update(It.Is<Airplane>(a => a.Id == id && a.Model == "Updated" && a.SeatCapacity == 200 && a.AirlineId == 2)), Times.Once);
            _unitOfWorkMock.Verify(u => u.CompleteAsync(), Times.Once);
            _cacheServiceMock.Verify(c => c.RemoveAsync("airplanes:all"), Times.Once);
            _cacheServiceMock.Verify(c => c.RemoveAsync($"airplane:id:{id}"), Times.Once);
            _cacheServiceMock.Verify(c => c.RemoveAsync($"airplane:with-airline:id:{id}"), Times.Once);
        }

        [Fact]
        public async Task Edit_Post_Exception_ShouldReturnViewWithModelErrorAndDropdown()
        {
            // Arrange
            int id = 1;
            var airplaneVM = new AirplaneViewModel { Id = id, Model = "Test", SeatCapacity = 100, AirlineId = 1 };
            var exception = new Exception("Update failed");

            var existingAirplane = new Airplane { Id = id, Model = "Old", SeatCapacity = 80, AirlineId = 1 };
            _airplaneRepositoryMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(existingAirplane);

            _unitOfWorkMock.Setup(u => u.CompleteAsync()).ThrowsAsync(exception);

            var airlines = new List<Airline>
            {
                new Airline { Id = 1, Name = "Airline1" }
            };
            var selectListItems = airlines.Select(a => new SelectListItem { Text = a.Name, Value = a.Id.ToString() }).ToList();

            _cacheServiceMock.Setup(c => c.GetOrSetAsync(
                    It.Is<string>(key => key == "airlines:dropdown"),
                    It.IsAny<Func<Task<List<SelectListItem>>>>(),
                    It.IsAny<TimeSpan>()))
                .ReturnsAsync(selectListItems);

            // Act
            var result = await _controller.Edit(id, airplaneVM);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal(airplaneVM, viewResult.Model);
            Assert.False(_controller.ModelState.IsValid);
            Assert.Contains("An error occurred while updating the Airplane.", _controller.ModelState[string.Empty].Errors[0].ErrorMessage);
            Assert.Equal(selectListItems, _controller.ViewBag.Airlines);
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
            _airplaneRepositoryMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync((Airplane)null);

            // Act
            var result = await _controller.Delete(id);

            // Assert
            var jsonResult = Assert.IsType<JsonResult>(result);
            var jsonString = JsonSerializer.Serialize(jsonResult.Value);
            using var jsonDoc = JsonDocument.Parse(jsonString);
            var root = jsonDoc.RootElement;

            Assert.False(root.GetProperty("success").GetBoolean());
            Assert.Equal("Airplane not found", root.GetProperty("message").GetString());
        }

        [Fact]
        public async Task Delete_Valid_ShouldDeleteAndReturnSuccess_AndInvalidateCache_AndSendHubNotification()
        {
            // Arrange
            int id = 1;
            var airplaneEntity = new Airplane { Id = id, Model = "Test", SeatCapacity = 100, AirlineId = 1 };
            _airplaneRepositoryMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(airplaneEntity);
            _unitOfWorkMock.Setup(u => u.CompleteAsync()).ReturnsAsync(1);

            _cacheServiceMock.Setup(c => c.RemoveAsync("airplanes:all")).Returns(Task.CompletedTask);
            _cacheServiceMock.Setup(c => c.RemoveAsync($"airplane:id:{id}")).Returns(Task.CompletedTask);
            _cacheServiceMock.Setup(c => c.RemoveAsync($"airplane:with-airline:id:{id}")).Returns(Task.CompletedTask);

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

            _airplaneRepositoryMock.Verify(r => r.Delete(airplaneEntity), Times.Once);
            _unitOfWorkMock.Verify(u => u.CompleteAsync(), Times.Once);
            _cacheServiceMock.Verify(c => c.RemoveAsync("airplanes:all"), Times.Once);
            _cacheServiceMock.Verify(c => c.RemoveAsync($"airplane:id:{id}"), Times.Once);
            _cacheServiceMock.Verify(c => c.RemoveAsync($"airplane:with-airline:id:{id}"), Times.Once);
            clientProxyMock.Verify(c => c.SendCoreAsync("ReceiveUpdate", It.IsAny<object[]>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Delete_Exception_ShouldReturnJsonError()
        {
            // Arrange
            int id = 1;
            var airplaneEntity = new Airplane { Id = id, Model = "Test", SeatCapacity = 100, AirlineId = 1 };
            _airplaneRepositoryMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(airplaneEntity);
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
    }
}