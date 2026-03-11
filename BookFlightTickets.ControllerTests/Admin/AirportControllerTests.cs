using BookFlightTickets.Core.Domain.Entities;
using BookFlightTickets.Core.Domain.RepositoryContracts;
using BookFlightTickets.Core.Domain.Specifications;
using BookFlightTickets.Core.ServiceContracts;
using BookFlightTickets.Core.ViewModels;
using BookFlightTickets.UI.Areas.Admin.Controllers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;

namespace BookFlightTickets.ControllerTests.Admin
{
    public class AirportControllerTests
    {
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly Mock<IGenericRepository<Airport>> _repositoryMock;
        private readonly Mock<IRedisCacheService> _cacheServiceMock;
        private readonly Mock<IWebHostEnvironment> _envMock;
        private readonly Mock<ILogger<AirportController>> _loggerMock;
        private readonly AirportController _controller;
        private readonly ITempDataDictionary _tempData;

        public AirportControllerTests()
        {
            _unitOfWorkMock = new Mock<IUnitOfWork>();
            _repositoryMock = new Mock<IGenericRepository<Airport>>();
            _cacheServiceMock = new Mock<IRedisCacheService>();
            _envMock = new Mock<IWebHostEnvironment>();
            _loggerMock = new Mock<ILogger<AirportController>>();

            // Setup UnitOfWork to return the mocked repository
            _unitOfWorkMock.Setup(u => u.Repository<Airport>()).Returns(_repositoryMock.Object);

            // Create controller instance
            _controller = new AirportController(
                _unitOfWorkMock.Object,
                _envMock.Object,
                _cacheServiceMock.Object,
                _loggerMock.Object
            );

            // Setup TempData
            _tempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>());
            _controller.TempData = _tempData;
        }

        #region Index

        [Fact]
        public async Task Index_ShouldReturnViewWithAirports_WhenCacheHit()
        {
            // Arrange
            var airports = new List<AirportViewModel>
            {
                new AirportViewModel { Id = 1, Name = "JFK", Code = "JFK", City = "New York", Country = "USA" },
                new AirportViewModel { Id = 2, Name = "Heathrow", Code = "LHR", City = "London", Country = "UK" }
            };

            _cacheServiceMock.Setup(c => c.GetOrSetAsync(
                    It.Is<string>(key => key == "airports:all"),
                    It.IsAny<Func<Task<List<AirportViewModel>>>>(),
                    It.IsAny<TimeSpan>()))
                .ReturnsAsync(airports);

            // Act
            var result = await _controller.Index();

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<IEnumerable<AirportViewModel>>(viewResult.Model);
            Assert.Equal(2, model.Count());
            _cacheServiceMock.Verify(c => c.GetOrSetAsync("airports:all", It.IsAny<Func<Task<List<AirportViewModel>>>>(), It.IsAny<TimeSpan>()), Times.Once);
        }

        [Fact]
        public async Task Index_ShouldReturnViewWithAirports_WhenCacheMiss()
        {
            // Arrange
            var airportEntities = new List<Airport>
            {
                new Airport { Id = 1, Name = "JFK", Code = "JFK", City = "New York", Country = "USA" },
                new Airport { Id = 2, Name = "Heathrow", Code = "LHR", City = "London", Country = "UK" }
            };

            _repositoryMock
                .Setup(r => r.GetAllWithSpecAsync(It.IsAny<BaseSpecification<Airport>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(airportEntities);

            _cacheServiceMock
             .Setup(c => c.GetOrSetAsync(
                 It.Is<string>(key => key == "airports:all"),
                 It.IsAny<Func<Task<List<AirportViewModel>>>>(),
                 It.IsAny<TimeSpan>()))
             .Returns((string key, Func<Task<List<AirportViewModel>>> factory, TimeSpan expiry) => factory());

            // Act
            var result = await _controller.Index();

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<IEnumerable<AirportViewModel>>(viewResult.Model);
            Assert.Equal(2, model.Count());
            _repositoryMock.Verify(r => r.GetAllWithSpecAsync(It.IsAny<BaseSpecification<Airport>>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Index_ShouldReturnViewWithInfoMessage_WhenNoAirports()
        {
            // Arrange
            _cacheServiceMock.Setup(c => c.GetOrSetAsync(
                    It.IsAny<string>(),
                    It.IsAny<Func<Task<List<AirportViewModel>>>>(),
                    It.IsAny<TimeSpan>()))
                .ReturnsAsync(new List<AirportViewModel>());

            // Act
            var result = await _controller.Index();

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<IEnumerable<AirportViewModel>>(viewResult.Model);
            Assert.Empty(model);
            Assert.Equal("No airports available.", _controller.ViewBag.InfoMessage);
        }

        [Fact]
        public async Task Index_ShouldReturnViewWithError_WhenExceptionOccurs()
        {
            // Arrange
            _cacheServiceMock.Setup(c => c.GetOrSetAsync(
                    It.IsAny<string>(),
                    It.IsAny<Func<Task<List<AirportViewModel>>>>(),
                    It.IsAny<TimeSpan>()))
                .ThrowsAsync(new Exception("Test exception"));

            // Act
            var result = await _controller.Index();

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<IEnumerable<AirportViewModel>>(viewResult.Model);
            Assert.Empty(model);
            Assert.Equal("An error occurred while retrieving airports.", _controller.ViewBag.ErrorMessage);
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
            var airportVM = new AirportViewModel { Name = "Test", Code = "TST", City = "City", Country = "Country" };
            _controller.ModelState.AddModelError("Name", "Required");

            // Act
            var result = await _controller.Create(airportVM);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal(airportVM, viewResult.Model);
            _repositoryMock.Verify(r => r.AddAsync(It.IsAny<Airport>()), Times.Never);
        }

        [Fact]
        public async Task Create_Post_ValidModel_ShouldAddAndRedirect_AndInvalidateCache()
        {
            // Arrange
            var airportVM = new AirportViewModel { Id = 0, Name = "New Airport", Code = "NAP", City = "City", Country = "Country" };
            var airportEntity = (Airport)airportVM;

            _repositoryMock.Setup(r => r.AddAsync(It.IsAny<Airport>()))
                .Callback<Airport>(a => a.Id = 1) // Simulate setting ID after add
                .Returns(Task.CompletedTask);

            _unitOfWorkMock.Setup(u => u.CompleteAsync()).ReturnsAsync(1);

            _cacheServiceMock.Setup(c => c.RemoveAsync("airports:all")).Returns(Task.CompletedTask);
            _cacheServiceMock.Setup(c => c.RemoveByPatternAsync("airport:id:*")).Returns(Task.CompletedTask);
            _cacheServiceMock.Setup(c => c.RemoveByPatternAsync("airport:details:id:*")).Returns(Task.CompletedTask);

            // Act
            var result = await _controller.Create(airportVM);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(nameof(AirportController.Index), redirectResult.ActionName);
            Assert.Equal("Airport has been Added Successfully", _controller.TempData["success"]);
            _repositoryMock.Verify(r => r.AddAsync(It.Is<Airport>(a => a.Name == airportVM.Name && a.Code == airportVM.Code && a.City == airportVM.City && a.Country == airportVM.Country)), Times.Once);
            _unitOfWorkMock.Verify(u => u.CompleteAsync(), Times.Once);
            _cacheServiceMock.Verify(c => c.RemoveAsync("airports:all"), Times.Once);
            _cacheServiceMock.Verify(c => c.RemoveByPatternAsync("airport:id:*"), Times.Once);
            _cacheServiceMock.Verify(c => c.RemoveByPatternAsync("airport:details:id:*"), Times.Once);
        }

        [Fact]
        public async Task Create_Post_WhenSaveFails_ShouldReturnViewWithModelError()
        {
            // Arrange
            var airportVM = new AirportViewModel { Name = "Test", Code = "TST", City = "City", Country = "Country" };
            _unitOfWorkMock.Setup(u => u.CompleteAsync()).ReturnsAsync(0);

            // Act
            var result = await _controller.Create(airportVM);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal(airportVM, viewResult.Model);
            Assert.False(_controller.ModelState.IsValid);
            Assert.Contains("Failed to save Airport.", _controller.ModelState[string.Empty].Errors[0].ErrorMessage);
        }

        [Fact]
        public async Task Create_Post_Exception_ShouldRedirectToIndexWithError()
        {
            // Arrange
            var airportVM = new AirportViewModel { Name = "Test", Code = "TST", City = "City", Country = "Country" };
            _repositoryMock.Setup(r => r.AddAsync(It.IsAny<Airport>()))
                .ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await _controller.Create(airportVM);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(nameof(AirportController.Index), redirectResult.ActionName);
            Assert.Equal("An error occurred while creating the Airport.", _controller.TempData["error"]);
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
            _cacheServiceMock.Setup(c => c.GetAsync<AirportViewModel>(It.IsAny<string>())).ReturnsAsync((AirportViewModel)null);
            _repositoryMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync((Airport)null);

            // Act
            var result = await _controller.Edit(id);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task Edit_Get_CacheHit_ShouldReturnViewWithAirport()
        {
            // Arrange
            int id = 1;
            var airportVM = new AirportViewModel { Id = id, Name = "Cached", Code = "CAC", City = "Cached City", Country = "Cached Country" };
            _cacheServiceMock.Setup(c => c.GetAsync<AirportViewModel>($"airport:id:{id}")).ReturnsAsync(airportVM);

            // Act
            var result = await _controller.Edit(id);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<AirportViewModel>(viewResult.Model);
            Assert.Equal(id, model.Id);
            _repositoryMock.Verify(r => r.GetByIdAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task Edit_Get_CacheMiss_ShouldGetFromDbAndCache_AndReturnView()
        {
            // Arrange
            int id = 1;
            var airportEntity = new Airport { Id = id, Name = "FromDb", Code = "FDB", City = "Db City", Country = "Db Country" };
            var expectedVM = (AirportViewModel)airportEntity;

            _cacheServiceMock.Setup(c => c.GetAsync<AirportViewModel>($"airport:id:{id}")).ReturnsAsync((AirportViewModel)null);
            _repositoryMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(airportEntity);
            _cacheServiceMock.Setup(c => c.SetAsync($"airport:id:{id}", expectedVM, It.IsAny<TimeSpan>())).Returns(Task.CompletedTask);

            // Act
            var result = await _controller.Edit(id);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<AirportViewModel>(viewResult.Model);
            Assert.Equal(id, model.Id);
            _repositoryMock.Verify(r => r.GetByIdAsync(id), Times.Once);
            _cacheServiceMock.Verify(c => c.SetAsync(
               $"airport:id:{id}",
               It.Is<AirportViewModel>(vm => vm.Id == id && vm.Name == "FromDb" && vm.Code == "FDB" && vm.City == "Db City" && vm.Country == "Db Country"),
               It.IsAny<TimeSpan>()),
               Times.Once);
        }

        [Fact]
        public async Task Edit_Get_Exception_ShouldRedirectToIndexWithError()
        {
            // Arrange
            int id = 1;
            _cacheServiceMock.Setup(c => c.GetAsync<AirportViewModel>(It.IsAny<string>())).ThrowsAsync(new Exception("Cache error"));

            // Act
            var result = await _controller.Edit(id);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(nameof(AirportController.Index), redirectResult.ActionName);
            Assert.Equal("An error occurred while retrieving the Airport.", _controller.TempData["error"]);
        }

        #endregion

        #region Edit (POST)

        [Fact]
        public async Task Edit_Post_IdMismatch_ShouldReturnBadRequest()
        {
            // Arrange
            int routeId = 1;
            var airportVM = new AirportViewModel { Id = 2, Name = "Test", Code = "TST", City = "City", Country = "Country" };

            // Act
            var result = await _controller.Edit(routeId, airportVM);

            // Assert
            Assert.IsType<BadRequestResult>(result);
        }

        [Fact]
        public async Task Edit_Post_InvalidModel_ShouldReturnView()
        {
            // Arrange
            int id = 1;
            var airportVM = new AirportViewModel { Id = id, Name = "Test", Code = "TST", City = "City", Country = "Country" };
            _controller.ModelState.AddModelError("Name", "Required");

            // Act
            var result = await _controller.Edit(id, airportVM);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal(airportVM, viewResult.Model);
            _unitOfWorkMock.Verify(u => u.CompleteAsync(), Times.Never);
        }

        [Fact]
        public async Task Edit_Post_ValidModel_ShouldUpdateAndRedirect_AndInvalidateCache()
        {
            // Arrange
            int id = 1;
            var airportVM = new AirportViewModel { Id = id, Name = "Updated", Code = "UPD", City = "Updated City", Country = "Updated Country" };
            var existingAirport = new Airport { Id = id, Name = "Old", Code = "OLD", City = "Old City", Country = "Old Country" };

            var airportRepositoryMock = new Mock<IGenericRepository<Airport>>();
            airportRepositoryMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(existingAirport);
            airportRepositoryMock.Setup(r => r.Update(It.IsAny<Airport>())).Verifiable();
            _unitOfWorkMock.Setup(u => u.Repository<Airport>()).Returns(airportRepositoryMock.Object);

            _unitOfWorkMock.Setup(u => u.CompleteAsync()).ReturnsAsync(1);

            _cacheServiceMock.Setup(c => c.RemoveAsync("airports:all")).Returns(Task.CompletedTask);
            _cacheServiceMock.Setup(c => c.RemoveAsync($"airport:id:{id}")).Returns(Task.CompletedTask);
            _cacheServiceMock.Setup(c => c.RemoveAsync($"airport:details:id:{id}")).Returns(Task.CompletedTask);

            // Act
            var result = await _controller.Edit(id, airportVM);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(nameof(AirportController.Index), redirectResult.ActionName);
            Assert.Equal("Airport Updated Successfully", _controller.TempData["success"]);
            Assert.Equal("Updated", existingAirport.Name);
            Assert.Equal("UPD", existingAirport.Code);
            Assert.Equal("Updated City", existingAirport.City);
            Assert.Equal("Updated Country", existingAirport.Country);
            airportRepositoryMock.Verify(r => r.Update(existingAirport), Times.Once);
            _unitOfWorkMock.Verify(u => u.CompleteAsync(), Times.Once);
            _cacheServiceMock.Verify(c => c.RemoveAsync("airports:all"), Times.Once);
            _cacheServiceMock.Verify(c => c.RemoveAsync($"airport:id:{id}"), Times.Once);
            _cacheServiceMock.Verify(c => c.RemoveAsync($"airport:details:id:{id}"), Times.Once);
        }

        [Fact]
        public async Task Edit_Post_Exception_ShouldReturnViewWithModelError_InDevelopment()
        {
            // Arrange
            int id = 1;
            var airportVM = new AirportViewModel { Id = id, Name = "Test", Code = "TST", City = "City", Country = "Country" };
            var existingAirport = new Airport { Id = id, Name = "Old", Code = "OLD", City = "Old City", Country = "Old Country" };
            var exception = new Exception("Update failed");

            var airportRepositoryMock = new Mock<IGenericRepository<Airport>>();
            airportRepositoryMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(existingAirport);
            _unitOfWorkMock.Setup(u => u.Repository<Airport>()).Returns(airportRepositoryMock.Object);

            _envMock.Setup(e => e.EnvironmentName).Returns("Development");
            _unitOfWorkMock.Setup(u => u.CompleteAsync()).ThrowsAsync(exception);

            // Act
            var result = await _controller.Edit(id, airportVM);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal(airportVM, viewResult.Model);
            Assert.False(_controller.ModelState.IsValid);
            Assert.Contains("Update failed", _controller.ModelState[string.Empty].Errors[0].ErrorMessage);
            _cacheServiceMock.Verify(c => c.RemoveAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task Edit_Post_Exception_ShouldReturnViewWithGenericError_InProduction()
        {
            // Arrange
            int id = 1;
            var airportVM = new AirportViewModel { Id = id, Name = "Test", Code = "TST", City = "City", Country = "Country" };
            var existingAirport = new Airport { Id = id, Name = "Old", Code = "OLD", City = "Old City", Country = "Old Country" };
            var exception = new Exception("Update failed");

            var airportRepositoryMock = new Mock<IGenericRepository<Airport>>();
            airportRepositoryMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(existingAirport);
            _unitOfWorkMock.Setup(u => u.Repository<Airport>()).Returns(airportRepositoryMock.Object);
            _unitOfWorkMock.Setup(u => u.CompleteAsync()).ThrowsAsync(exception);

            _envMock.Setup(e => e.EnvironmentName).Returns("Production");

            // Act
            var result = await _controller.Edit(id, airportVM);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal(airportVM, viewResult.Model);
            Assert.False(_controller.ModelState.IsValid);
            Assert.Contains("An Error Has Occurred during Updating the Airport", _controller.ModelState[string.Empty].Errors[0].ErrorMessage);
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
            _repositoryMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync((Airport)null);

            // Act
            var result = await _controller.Delete(id);

            // Assert
            var jsonResult = Assert.IsType<JsonResult>(result);
            var jsonString = JsonSerializer.Serialize(jsonResult.Value);
            using var jsonDoc = JsonDocument.Parse(jsonString);
            var root = jsonDoc.RootElement;

            Assert.False(root.GetProperty("success").GetBoolean());
            Assert.Equal("Airport not found", root.GetProperty("message").GetString());
        }

        [Fact]
        public async Task Delete_Valid_ShouldDeleteAndReturnSuccess_AndInvalidateCache()
        {
            // Arrange
            int id = 1;
            var airportEntity = new Airport { Id = id, Name = "Test", Code = "TST", City = "City", Country = "Country" };
            _repositoryMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(airportEntity);
            _unitOfWorkMock.Setup(u => u.CompleteAsync()).ReturnsAsync(1);

            _cacheServiceMock.Setup(c => c.RemoveAsync("airports:all")).Returns(Task.CompletedTask);
            _cacheServiceMock.Setup(c => c.RemoveAsync($"airport:id:{id}")).Returns(Task.CompletedTask);
            _cacheServiceMock.Setup(c => c.RemoveAsync($"airport:details:id:{id}")).Returns(Task.CompletedTask);

            // Act
            var result = await _controller.Delete(id);

            // Assert
            var jsonResult = Assert.IsType<JsonResult>(result);
            var jsonString = JsonSerializer.Serialize(jsonResult.Value);
            using var jsonDoc = JsonDocument.Parse(jsonString);
            var root = jsonDoc.RootElement;

            Assert.True(root.GetProperty("success").GetBoolean());
            Assert.Equal("Delete Successful", root.GetProperty("message").GetString());

            _repositoryMock.Verify(r => r.Delete(airportEntity), Times.Once);
            _unitOfWorkMock.Verify(u => u.CompleteAsync(), Times.Once);
            _cacheServiceMock.Verify(c => c.RemoveAsync("airports:all"), Times.Once);
            _cacheServiceMock.Verify(c => c.RemoveAsync($"airport:id:{id}"), Times.Once);
            _cacheServiceMock.Verify(c => c.RemoveAsync($"airport:details:id:{id}"), Times.Once);
        }

        [Fact]
        public async Task Delete_Exception_ShouldReturnJsonError()
        {
            // Arrange
            int id = 1;
            var airportEntity = new Airport { Id = id, Name = "Test", Code = "TST", City = "City", Country = "Country" };
            _repositoryMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(airportEntity);
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