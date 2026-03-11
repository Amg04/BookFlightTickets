using BookFlightTickets.Core.Domain.Entities;
using BookFlightTickets.Core.Domain.Hubs;
using BookFlightTickets.Core.Domain.RepositoryContracts;
using BookFlightTickets.Core.ServiceContracts;
using BookFlightTickets.Core.Shared.Utility;
using BookFlightTickets.Core.ViewModels;
using BookFlightTickets.UI.Areas.Admin.Controllers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;

namespace BookFlightTickets.ControllerTests.Admin
{
    public class AirlineControllerTests
    {
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly Mock<IGenericRepository<Airline>> _repositoryMock;
        private readonly Mock<IRedisCacheService> _cacheServiceMock;
        private readonly Mock<IWebHostEnvironment> _envMock;
        private readonly Mock<ILogger<AirlineController>> _loggerMock;
        private readonly Mock<IHubContext<DashboardHub>> _hubContextMock;
        private readonly AirlineController _controller;
        private readonly ITempDataDictionary _tempData;

        public AirlineControllerTests()
        {
            _unitOfWorkMock = new Mock<IUnitOfWork>();
            _repositoryMock = new Mock<IGenericRepository<Airline>>();
            _cacheServiceMock = new Mock<IRedisCacheService>();
            _envMock = new Mock<IWebHostEnvironment>();
            _loggerMock = new Mock<ILogger<AirlineController>>();
            _hubContextMock = new Mock<IHubContext<DashboardHub>>();

            // Setup UnitOfWork to return the mocked repository
            _unitOfWorkMock.Setup(u => u.Repository<Airline>()).Returns(_repositoryMock.Object);

            // Create controller instance
            _controller = new AirlineController(
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
        public async Task Index_ShouldReturnViewWithAirlines_WhenCacheHit()
        {
            // Arrange
            var airlines = new List<AirlineViewModel>
            {
                new AirlineViewModel { Id = 1, Name = "Test1", Code = "T1" },
                new AirlineViewModel { Id = 2, Name = "Test2", Code = "T2" }
            };

            _cacheServiceMock.Setup(c => c.GetOrSetAsync(
                    It.Is<string>(key => key == "airlines:all"),
                    It.IsAny<Func<Task<List<AirlineViewModel>>>>(),
                    It.IsAny<TimeSpan>()))
                .ReturnsAsync(airlines);

            // Act
            var result = await _controller.Index();

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<IEnumerable<AirlineViewModel>>(viewResult.Model);
            Assert.Equal(2, model.Count());
            _cacheServiceMock.Verify(c => c.GetOrSetAsync("airlines:all", It.IsAny<Func<Task<List<AirlineViewModel>>>>(), It.IsAny<TimeSpan>()), Times.Once);
        }

        [Fact]
        public async Task Index_ShouldReturnViewWithAirlines_WhenCacheMiss()
        {
            // Arrange
            var airlineEntities = new List<Airline>
            {
                new Airline { Id = 1, Name = "Test1", Code = "T1" },
                new Airline { Id = 2, Name = "Test2", Code = "T2" }
            };
            var expectedViewModels = airlineEntities.Select(a => (AirlineViewModel)a).ToList();

            _repositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(airlineEntities);

            _cacheServiceMock.Setup(c => c.GetOrSetAsync(
                It.Is<string>(key => key == "airlines:all"),
                It.IsAny<Func<Task<List<AirlineViewModel>>>>(),
                It.IsAny<TimeSpan>()))
                .Returns((string key, Func<Task<List<AirlineViewModel>>> factory, TimeSpan expiry) => factory());

            // Act
            var result = await _controller.Index();

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<IEnumerable<AirlineViewModel>>(viewResult.Model);
            Assert.Equal(2, model.Count());
            _repositoryMock.Verify(r => r.GetAllAsync(), Times.Once);
        }

        [Fact]
        public async Task Index_ShouldReturnViewWithInfoMessage_WhenNoAirlines()
        {
            // Arrange
            _cacheServiceMock.Setup(c => c.GetOrSetAsync(
                    It.IsAny<string>(),
                    It.IsAny<Func<Task<List<AirlineViewModel>>>>(),
                    It.IsAny<TimeSpan>()))
                .ReturnsAsync(new List<AirlineViewModel>());

            // Act
            var result = await _controller.Index();

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<IEnumerable<AirlineViewModel>>(viewResult.Model);
            Assert.Empty(model);
            Assert.Equal("No airlines available.", _controller.ViewBag.InfoMessage);
        }

        [Fact]
        public async Task Index_ShouldReturnViewWithError_WhenExceptionOccurs()
        {
            // Arrange
            _cacheServiceMock.Setup(c => c.GetOrSetAsync(
                    It.IsAny<string>(),
                    It.IsAny<Func<Task<List<AirlineViewModel>>>>(),
                    It.IsAny<TimeSpan>()))
                .ThrowsAsync(new Exception("Test exception"));

            // Act
            var result = await _controller.Index();

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<IEnumerable<AirlineViewModel>>(viewResult.Model);
            Assert.Empty(model);
            Assert.Equal("An error occurred while retrieving airlines.", _controller.ViewBag.ErrorMessage);
        }

        #endregion

        #region Create (GET)

        [Fact]
        public void Create_Get_ShouldReturnView()
        {
            // Act
            var result = _controller.Create();

            // Assert
            Assert.IsType<ViewResult>(result);
        }

        #endregion

        #region Create (POST)

        [Fact]
        public async Task Create_Post_InvalidModel_ShouldReturnViewWithModel()
        {
            // Arrange
            var airlineVM = new AirlineViewModel { Name = "Test", Code = "T1" };
            _controller.ModelState.AddModelError("Name", "Required");

            // Act
            var result = await _controller.Create(airlineVM);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal(airlineVM, viewResult.Model);
            _repositoryMock.Verify(r => r.AddAsync(It.IsAny<Airline>()), Times.Never);
        }

        [Fact]
        public async Task Create_Post_ValidModel_ShouldAddAndRedirect_AndInvalidateCache_AndSendHubNotification()
        {
            // Arrange
            var airlineVM = new AirlineViewModel { Id = 0, Name = "New Airline", Code = "NA" };
            var airlineEntity = (Airline)airlineVM;

            _repositoryMock.Setup(r => r.AddAsync(It.IsAny<Airline>()))
                .Callback<Airline>(a => a.Id = 1) // Simulate setting ID after add
                .Returns(Task.CompletedTask);

            _unitOfWorkMock.Setup(u => u.CompleteAsync()).ReturnsAsync(1);

            _cacheServiceMock.Setup(c => c.RemoveAsync("airlines:all")).Returns(Task.CompletedTask);
            _cacheServiceMock.Setup(c => c.RemoveByPatternAsync("airline:id:*")).Returns(Task.CompletedTask);
            _cacheServiceMock.Setup(c => c.RemoveByPatternAsync("airline:details:id:*")).Returns(Task.CompletedTask);

            var clientsMock = new Mock<IHubClients>();
            var clientProxyMock = new Mock<IClientProxy>();
            clientsMock.Setup(c => c.Group(SD.Admin)).Returns(clientProxyMock.Object);
            _hubContextMock.Setup(h => h.Clients).Returns(clientsMock.Object);
            clientProxyMock.Setup(c => c.SendCoreAsync("ReceiveUpdate", It.IsAny<object[]>(), default)).Returns(Task.CompletedTask);

            // Act
            var result = await _controller.Create(airlineVM);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(nameof(AirlineController.Index), redirectResult.ActionName);
            Assert.Equal("Airline has been Added Successfully", _controller.TempData["success"]);
            _repositoryMock.Verify(r => r.AddAsync(It.Is<Airline>(a => a.Name == airlineVM.Name && a.Code == airlineVM.Code)), Times.Once);
            _unitOfWorkMock.Verify(u => u.CompleteAsync(), Times.Once);
            _cacheServiceMock.Verify(c => c.RemoveAsync("airlines:all"), Times.Once);
            _cacheServiceMock.Verify(c => c.RemoveByPatternAsync("airline:id:*"), Times.Once);
            _cacheServiceMock.Verify(c => c.RemoveByPatternAsync("airline:details:id:*"), Times.Once);
            clientProxyMock.Verify(c => c.SendCoreAsync("ReceiveUpdate", It.IsAny<object[]>(), default), Times.Once);
        }

        [Fact]
        public async Task Create_Post_WhenSaveFails_ShouldReturnViewWithModelError()
        {
            // Arrange
            var airlineVM = new AirlineViewModel { Name = "Test", Code = "T1" };
            _unitOfWorkMock.Setup(u => u.CompleteAsync()).ReturnsAsync(0);

            // Act
            var result = await _controller.Create(airlineVM);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal(airlineVM, viewResult.Model);
            Assert.False(_controller.ModelState.IsValid);
            Assert.Contains("Failed to save Airline.", _controller.ModelState[string.Empty].Errors[0].ErrorMessage);
        }

        [Fact]
        public async Task Create_Post_Exception_ShouldReturnViewWithModelError()
        {
            // Arrange
            var airlineVM = new AirlineViewModel { Name = "Test", Code = "T1" };
            _repositoryMock.Setup(r => r.AddAsync(It.IsAny<Airline>()))
                .ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await _controller.Create(airlineVM);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal(airlineVM, viewResult.Model);
            Assert.False(_controller.ModelState.IsValid);
            Assert.Contains("An error occurred while creating the Airline.", _controller.ModelState[string.Empty].Errors[0].ErrorMessage);
        }

        [Fact]
        public async Task Create_Post_HubNotificationException_ShouldLogErrorButNotFail()
        {
            // Arrange
            var airlineVM = new AirlineViewModel { Id = 0, Name = "New Airline", Code = "NA" };
            var airlineEntity = (Airline)airlineVM;

            _repositoryMock.Setup(r => r.AddAsync(It.IsAny<Airline>()))
                .Callback<Airline>(a => a.Id = 1)
                .Returns(Task.CompletedTask);

            _unitOfWorkMock.Setup(u => u.CompleteAsync()).ReturnsAsync(1);
            _cacheServiceMock.Setup(c => c.RemoveAsync("airlines:all")).Returns(Task.CompletedTask);

            var clientsMock = new Mock<IHubClients>();
            var clientProxyMock = new Mock<IClientProxy>();
            clientsMock.Setup(c => c.Group(SD.Admin)).Returns(clientProxyMock.Object);
            _hubContextMock.Setup(h => h.Clients).Returns(clientsMock.Object);
            clientProxyMock.Setup(c => c.SendCoreAsync("ReceiveUpdate", It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("SignalR error"));

            // Act
            var result = await _controller.Create(airlineVM);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(nameof(AirlineController.Index), redirectResult.ActionName);
            Assert.Equal("Airline has been Added Successfully", _controller.TempData["success"]);
            _repositoryMock.Verify(r => r.AddAsync(It.IsAny<Airline>()), Times.Once);
            _unitOfWorkMock.Verify(u => u.CompleteAsync(), Times.Once);
            clientProxyMock.Verify(c => c.SendCoreAsync("ReceiveUpdate", It.IsAny<object[]>(), default), Times.Once);
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
            _cacheServiceMock.Setup(c => c.GetAsync<AirlineViewModel>(It.IsAny<string>())).ReturnsAsync((AirlineViewModel)null);
            _repositoryMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync((Airline)null);

            // Act
            var result = await _controller.Edit(id);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task Edit_Get_CacheHit_ShouldReturnViewWithAirline()
        {
            // Arrange
            int id = 1;
            var airlineVM = new AirlineViewModel { Id = id, Name = "Cached", Code = "C1" };
            _cacheServiceMock.Setup(c => c.GetAsync<AirlineViewModel>($"airline:id:{id}")).ReturnsAsync(airlineVM);

            // Act
            var result = await _controller.Edit(id);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<AirlineViewModel>(viewResult.Model);
            Assert.Equal(id, model.Id);
            _repositoryMock.Verify(r => r.GetByIdAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task Edit_Get_CacheMiss_ShouldGetFromDbAndCache_AndReturnView()
        {
            // Arrange
            int id = 1;
            var airlineEntity = new Airline { Id = id, Name = "FromDb", Code = "F1" };
            var expectedVM = (AirlineViewModel)airlineEntity;

            _cacheServiceMock.Setup(c => c.GetAsync<AirlineViewModel>($"airline:id:{id}")).ReturnsAsync((AirlineViewModel)null);
            _repositoryMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(airlineEntity);
            _cacheServiceMock.Setup(c => c.SetAsync($"airline:id:{id}", expectedVM, It.IsAny<TimeSpan>())).Returns(Task.CompletedTask);

            // Act
            var result = await _controller.Edit(id);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<AirlineViewModel>(viewResult.Model);
            Assert.Equal(id, model.Id);
            _repositoryMock.Verify(r => r.GetByIdAsync(id), Times.Once);
            _cacheServiceMock.Verify(c => c.SetAsync(
               $"airline:id:{id}",
               It.Is<AirlineViewModel>(vm => vm.Id == id && vm.Name == "FromDb" && vm.Code == "F1"),
               It.IsAny<TimeSpan>()),
               Times.Once);
        }

        [Fact]
        public async Task Edit_Get_Exception_ShouldRedirectToIndexWithError()
        {
            // Arrange
            int id = 1;
            _cacheServiceMock.Setup(c => c.GetAsync<AirlineViewModel>(It.IsAny<string>())).ThrowsAsync(new Exception("Cache error"));

            // Act
            var result = await _controller.Edit(id);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(nameof(AirlineController.Index), redirectResult.ActionName);
            Assert.Equal("An error occurred while retrieving the Airline.", _controller.TempData["error"]);
        }

        #endregion

        #region Edit (POST)

        [Fact]
        public async Task Edit_Post_IdMismatch_ShouldReturnNotFound()
        {
            // Arrange
            int routeId = 1;
            var airlineVM = new AirlineViewModel { Id = 2, Name = "Test", Code = "T1" };

            // Act
            var result = await _controller.Edit(routeId, airlineVM);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task Edit_Post_InvalidModel_ShouldReturnView()
        {
            // Arrange
            int id = 1;
            var airlineVM = new AirlineViewModel { Id = id, Name = "Test", Code = "T1" };
            _controller.ModelState.AddModelError("Name", "Required");

            // Act
            var result = await _controller.Edit(id, airlineVM);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal(airlineVM, viewResult.Model);
            _unitOfWorkMock.Verify(u => u.CompleteAsync(), Times.Never);
        }

        [Fact]
        public async Task Edit_Post_ValidModel_ShouldUpdateAndRedirect_AndInvalidateCache_AndSendHubNotification()
        {
            // Arrange
            int id = 1;
            var airlineVM = new AirlineViewModel { Id = id, Name = "Updated", Code = "U1" };
            var existingAirline = new Airline { Id = id, Name = "Old", Code = "O1" };

            _repositoryMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(existingAirline);
            _unitOfWorkMock.Setup(u => u.CompleteAsync()).ReturnsAsync(1);

            _cacheServiceMock.Setup(c => c.RemoveAsync("airlines:all")).Returns(Task.CompletedTask);
            _cacheServiceMock.Setup(c => c.RemoveAsync($"airline:id:{id}")).Returns(Task.CompletedTask);
            _cacheServiceMock.Setup(c => c.RemoveAsync($"airline:details:id:{id}")).Returns(Task.CompletedTask);

            var clientsMock = new Mock<IHubClients>();
            var clientProxyMock = new Mock<IClientProxy>();
            clientsMock.Setup(c => c.Group(SD.Admin)).Returns(clientProxyMock.Object);
            _hubContextMock.Setup(h => h.Clients).Returns(clientsMock.Object);
            clientProxyMock.Setup(c => c.SendCoreAsync("ReceiveUpdate", It.IsAny<object[]>(), default)).Returns(Task.CompletedTask);

            // Act
            var result = await _controller.Edit(id, airlineVM);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(nameof(AirlineController.Index), redirectResult.ActionName);
            Assert.Equal("Airline Updated Successfully", _controller.TempData["success"]);
            Assert.Equal("Updated", existingAirline.Name);
            Assert.Equal("U1", existingAirline.Code);
            _repositoryMock.Verify(r => r.Update(existingAirline), Times.Once);
            _repositoryMock.Verify(r => r.Update(It.Is<Airline>(a => a.Id == id && a.Name == "Updated" && a.Code == "U1")), Times.Once);
            _unitOfWorkMock.Verify(u => u.CompleteAsync(), Times.Once);
            _cacheServiceMock.Verify(c => c.RemoveAsync("airlines:all"), Times.Once);
            _cacheServiceMock.Verify(c => c.RemoveAsync($"airline:id:{id}"), Times.Once);
            _cacheServiceMock.Verify(c => c.RemoveAsync($"airline:details:id:{id}"), Times.Once);
            clientProxyMock.Verify(c => c.SendCoreAsync("ReceiveUpdate", It.IsAny<object[]>(), default), Times.Once);
        }

        [Fact]
        public async Task Edit_Post_Exception_ShouldReturnViewWithModelError_InDevelopment()
        {
            // Arrange
            int id = 1;
            var airlineVM = new AirlineViewModel { Id = id, Name = "Test", Code = "T1" };
            var exception = new Exception("Update failed");

            var existingAirline = new Airline { Id = id, Name = "Old", Code = "O1" };
            _repositoryMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(existingAirline);

            _envMock.Setup(e => e.EnvironmentName).Returns("Development");
            _unitOfWorkMock.Setup(u => u.CompleteAsync()).ThrowsAsync(exception);

            // Act
            var result = await _controller.Edit(id, airlineVM);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal(airlineVM, viewResult.Model);
            Assert.False(_controller.ModelState.IsValid);
            Assert.Contains("Update failed", _controller.ModelState[string.Empty].Errors[0].ErrorMessage);
        }

        [Fact]
        public async Task Edit_Post_Exception_ShouldReturnViewWithGenericError_InProduction()
        {
            // Arrange
            int id = 1;
            var airlineVM = new AirlineViewModel { Id = id, Name = "Test", Code = "T1" };
            var exception = new Exception("Update failed");

            var existingAirline = new Airline { Id = id, Name = "Old", Code = "O1" };
            _repositoryMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(existingAirline);

            _envMock.Setup(e => e.EnvironmentName).Returns("Production");
            _unitOfWorkMock.Setup(u => u.CompleteAsync()).ThrowsAsync(exception);

            // Act
            var result = await _controller.Edit(id, airlineVM);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal(airlineVM, viewResult.Model);
            Assert.False(_controller.ModelState.IsValid);
            Assert.Contains("An Error Has Occurred during Updating the Airline", _controller.ModelState[string.Empty].Errors[0].ErrorMessage);
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
            _cacheServiceMock.Setup(c => c.GetAsync<AirlineViewModel>(It.IsAny<string>())).ReturnsAsync((AirlineViewModel)null);
            _repositoryMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync((Airline)null);

            // Act
            var result = await _controller.Details(id);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task Details_CacheHit_ShouldReturnViewWithAirline()
        {
            // Arrange
            int id = 1;
            var airlineVM = new AirlineViewModel { Id = id, Name = "Cached", Code = "C1" };
            _cacheServiceMock.Setup(c => c.GetAsync<AirlineViewModel>($"airline:details:id:{id}")).ReturnsAsync(airlineVM);

            // Act
            var result = await _controller.Details(id);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<AirlineViewModel>(viewResult.Model);
            Assert.Equal(id, model.Id);
            _repositoryMock.Verify(r => r.GetByIdAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task Details_CacheMiss_ShouldGetFromDbAndCacheBothKeys_AndReturnView()
        {
            // Arrange
            int id = 1;
            var airlineEntity = new Airline { Id = id, Name = "FromDb", Code = "F1" };
            var expectedVM = (AirlineViewModel)airlineEntity;

            _cacheServiceMock.Setup(c => c.GetAsync<AirlineViewModel>($"airline:details:id:{id}")).ReturnsAsync((AirlineViewModel)null);
            _repositoryMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(airlineEntity);
            _cacheServiceMock.Setup(c => c.SetAsync($"airline:details:id:{id}", expectedVM, It.IsAny<TimeSpan>())).Returns(Task.CompletedTask);
            _cacheServiceMock.Setup(c => c.SetAsync($"airline:id:{id}", expectedVM, It.IsAny<TimeSpan>())).Returns(Task.CompletedTask);

            // Act
            var result = await _controller.Details(id);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<AirlineViewModel>(viewResult.Model);
            Assert.Equal(id, model.Id);
            _repositoryMock.Verify(r => r.GetByIdAsync(id), Times.Once);
            _cacheServiceMock.Verify(c => c.SetAsync($"airline:details:id:{id}",
                It.Is<AirlineViewModel>(vm => vm.Id == id && vm.Name == "FromDb" && vm.Code == "F1"),
                It.IsAny<TimeSpan>()), Times.Once);
            _cacheServiceMock.Verify(c => c.SetAsync($"airline:id:{id}",
                It.Is<AirlineViewModel>(vm => vm.Id == id && vm.Name == "FromDb" && vm.Code == "F1"),
                It.IsAny<TimeSpan>()), Times.Once);
        }

        [Fact]
        public async Task Details_Exception_ShouldRedirectToIndexWithError()
        {
            // Arrange
            int id = 1;
            _cacheServiceMock.Setup(c => c.GetAsync<AirlineViewModel>(It.IsAny<string>())).ThrowsAsync(new Exception("Cache error"));

            // Act
            var result = await _controller.Details(id);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(nameof(AirlineController.Index), redirectResult.ActionName);
            Assert.Equal("An error occurred while retrieving the Airline details.", _controller.TempData["error"]);
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
            _repositoryMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync((Airline)null);

            // Act
            var result = await _controller.Delete(id);

            // Assert
            var jsonResult = Assert.IsType<JsonResult>(result);
            var jsonString = JsonSerializer.Serialize(jsonResult.Value);
            using var jsonDoc = JsonDocument.Parse(jsonString);
            var root = jsonDoc.RootElement;

            Assert.False(root.GetProperty("success").GetBoolean());
            Assert.Equal("Airline not found", root.GetProperty("message").GetString());
        }

        [Fact]
        public async Task Delete_Valid_ShouldDeleteAndReturnSuccess_AndInvalidateCache_AndSendHubNotification()
        {
            // Arrange
            int id = 1;
            var airlineEntity = new Airline { Id = id, Name = "Test", Code = "T1" };
            _repositoryMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(airlineEntity);
            _unitOfWorkMock.Setup(u => u.CompleteAsync()).ReturnsAsync(1);

            _cacheServiceMock.Setup(c => c.RemoveAsync("airlines:all")).Returns(Task.CompletedTask);
            _cacheServiceMock.Setup(c => c.RemoveAsync($"airline:id:{id}")).Returns(Task.CompletedTask);
            _cacheServiceMock.Setup(c => c.RemoveAsync($"airline:details:id:{id}")).Returns(Task.CompletedTask);

            var clientsMock = new Mock<IHubClients>();
            var clientProxyMock = new Mock<IClientProxy>();
            clientsMock.Setup(c => c.Group(SD.Admin)).Returns(clientProxyMock.Object);
            _hubContextMock.Setup(h => h.Clients).Returns(clientsMock.Object);
            clientProxyMock.Setup(c => c.SendCoreAsync("ReceiveUpdate", It.IsAny<object[]>(), default)).Returns(Task.CompletedTask);

            // Act
            var result = await _controller.Delete(id);

            // Assert
            var jsonResult = Assert.IsType<JsonResult>(result);
            var jsonString = JsonSerializer.Serialize(jsonResult.Value);
            using var jsonDoc = JsonDocument.Parse(jsonString);
            var root = jsonDoc.RootElement;

            Assert.True(root.GetProperty("success").GetBoolean());
            Assert.Equal("Delete Successful", root.GetProperty("message").GetString());

            _repositoryMock.Verify(r => r.Delete(airlineEntity), Times.Once);
            _unitOfWorkMock.Verify(u => u.CompleteAsync(), Times.Once);
            _cacheServiceMock.Verify(c => c.RemoveAsync("airlines:all"), Times.Once);
            _cacheServiceMock.Verify(c => c.RemoveAsync($"airline:id:{id}"), Times.Once);
            _cacheServiceMock.Verify(c => c.RemoveAsync($"airline:details:id:{id}"), Times.Once);
            clientProxyMock.Verify(c => c.SendCoreAsync("ReceiveUpdate", It.IsAny<object[]>(), default), Times.Once);
        }

        [Fact]
        public async Task Delete_Exception_ShouldReturnJsonError()
        {
            // Arrange
            int id = 1;
            var airlineEntity = new Airline { Id = id, Name = "Test", Code = "T1" };
            _repositoryMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(airlineEntity);
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