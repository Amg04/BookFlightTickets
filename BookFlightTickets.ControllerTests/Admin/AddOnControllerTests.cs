using BookFlightTickets.Core.Domain.Entities;
using BookFlightTickets.Core.Domain.RepositoryContracts;
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
    public class AddOnControllerTests
    {
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly Mock<IGenericRepository<AddOn>> _repositoryMock;
        private readonly Mock<IRedisCacheService> _cacheServiceMock;
        private readonly Mock<IWebHostEnvironment> _envMock;
        private readonly Mock<ILogger<AddOnController>> _loggerMock;
        private readonly AddOnController _controller;
        private readonly ITempDataDictionary _tempData;

        public AddOnControllerTests()
        {
            _unitOfWorkMock = new Mock<IUnitOfWork>();
            _repositoryMock = new Mock<IGenericRepository<AddOn>>();
            _cacheServiceMock = new Mock<IRedisCacheService>();
            _envMock = new Mock<IWebHostEnvironment>();
            _loggerMock = new Mock<ILogger<AddOnController>>();

            // Setup UnitOfWork to return the mocked repository
            _unitOfWorkMock.Setup(u => u.Repository<AddOn>()).Returns(_repositoryMock.Object);

            // Create controller instance
            _controller = new AddOnController(
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
        public async Task Index_ShouldReturnViewWithAddOns_WhenCacheHit()
        {
            // Arrange
            var addons = new List<AddOnViewModel>
            {
                new AddOnViewModel { Id = 1, Name = "Test1", Price = 10 },
                new AddOnViewModel { Id = 2, Name = "Test2", Price = 20 }
            };

            _cacheServiceMock.Setup(c => c.GetOrSetAsync(
                    It.Is<string>(key => key == "addons:all"),
                    It.IsAny<Func<Task<List<AddOnViewModel>>>>(),
                    It.IsAny<TimeSpan>()))
                .ReturnsAsync(addons);

            // Act
            var result = await _controller.Index();

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<IEnumerable<AddOnViewModel>>(viewResult.Model);
            Assert.Equal(2, model.Count());
            _cacheServiceMock.Verify(c => c.GetOrSetAsync("addons:all", It.IsAny<Func<Task<List<AddOnViewModel>>>>(), It.IsAny<TimeSpan>()), Times.Once);
        }

        [Fact]
        public async Task Index_ShouldReturnViewWithAddOns_WhenCacheMiss()
        {
            // Arrange
            var addonEntities = new List<AddOn>
            {
                new AddOn { Id = 1, Name = "Test1", Price = 10 },
                new AddOn { Id = 2, Name = "Test2", Price = 20 }
            };
            var expectedViewModels = addonEntities.Select(a => (AddOnViewModel)a).ToList();

            _repositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(addonEntities);

            _cacheServiceMock.Setup(c => c.GetOrSetAsync(
                It.Is<string>(key => key == "addons:all"),
                It.IsAny<Func<Task<List<AddOnViewModel>>>>(),
                It.IsAny<TimeSpan>()))
                .Returns((string key, Func<Task<List<AddOnViewModel>>> factory, TimeSpan expiry) => factory());

            // Act
            var result = await _controller.Index();

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<IEnumerable<AddOnViewModel>>(viewResult.Model);
            Assert.Equal(2, model.Count());
            _repositoryMock.Verify(r => r.GetAllAsync(), Times.Once);
        }
        [Fact]
        public async Task Index_ShouldReturnViewWithInfoMessage_WhenNoAddOns()
        {
            // Arrange
            _cacheServiceMock.Setup(c => c.GetOrSetAsync(
                    It.IsAny<string>(),
                    It.IsAny<Func<Task<List<AddOnViewModel>>>>(),
                    It.IsAny<TimeSpan>()))
                .ReturnsAsync(new List<AddOnViewModel>());

            // Act
            var result = await _controller.Index();

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<IEnumerable<AddOnViewModel>>(viewResult.Model);
            Assert.Empty(model);
            Assert.Equal("No addons available.", _controller.ViewBag.InfoMessage);
        }

        [Fact]
        public async Task Index_ShouldReturnViewWithError_WhenExceptionOccurs()
        {
            // Arrange
            _cacheServiceMock.Setup(c => c.GetOrSetAsync(
                    It.IsAny<string>(),
                    It.IsAny<Func<Task<List<AddOnViewModel>>>>(),
                    It.IsAny<TimeSpan>()))
                .ThrowsAsync(new Exception("Test exception"));

            // Act
            var result = await _controller.Index();

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<IEnumerable<AddOnViewModel>>(viewResult.Model);
            Assert.Empty(model);
            Assert.Equal("An error occurred while retrieving addons.", _controller.ViewBag.ErrorMessage);
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
            var addonVM = new AddOnViewModel { Name = "Test", Price = 10 };
            _controller.ModelState.AddModelError("Name", "Required");

            // Act
            var result = await _controller.Create(addonVM);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal(addonVM, viewResult.Model);
            _repositoryMock.Verify(r => r.AddAsync(It.IsAny<AddOn>()), Times.Never);
        }

        [Fact]
        public async Task Create_Post_ValidModel_ShouldAddAndRedirect_AndInvalidateCache()
        {
            // Arrange
            var addonVM = new AddOnViewModel { Id = 0, Name = "New AddOn", Price = 15 };
            var addonEntity = (AddOn)addonVM;

            _repositoryMock.Setup(r => r.AddAsync(It.IsAny<AddOn>()))
                .Callback<AddOn>(a => a.Id = 1) // Simulate setting ID after add
                .Returns(Task.CompletedTask);

            _unitOfWorkMock.Setup(u => u.CompleteAsync()).ReturnsAsync(1);

            _cacheServiceMock.Setup(c => c.RemoveAsync("addons:all")).Returns(Task.CompletedTask);

            // Act
            var result = await _controller.Create(addonVM);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(nameof(AddOnController.Index), redirectResult.ActionName);
            Assert.Equal("AddOn has been Added Successfully", _controller.TempData["success"]);
            _repositoryMock.Verify(r => r.AddAsync(It.Is<AddOn>(a => a.Name == addonVM.Name && a.Price == addonVM.Price)), Times.Once);
            _unitOfWorkMock.Verify(u => u.CompleteAsync(), Times.Once);
            _cacheServiceMock.Verify(c => c.RemoveAsync("addons:all"), Times.Once);
        }

        [Fact]
        public async Task Create_Post_WhenSaveFails_ShouldReturnViewWithModelError()
        {
            // Arrange
            var addonVM = new AddOnViewModel { Name = "Test", Price = 10 };
            _unitOfWorkMock.Setup(u => u.CompleteAsync()).ReturnsAsync(0);

            // Act
            var result = await _controller.Create(addonVM);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal(addonVM, viewResult.Model);
            Assert.False(_controller.ModelState.IsValid);
            Assert.Contains("Failed to save AddOn.", _controller.ModelState[string.Empty].Errors[0].ErrorMessage);
        }

        [Fact]
        public async Task Create_Post_Exception_ShouldReturnViewWithModelError()
        {
            // Arrange
            var addonVM = new AddOnViewModel { Name = "Test", Price = 10 };
            _repositoryMock.Setup(r => r.AddAsync(It.IsAny<AddOn>()))
                .ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await _controller.Create(addonVM);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal(addonVM, viewResult.Model);
            Assert.False(_controller.ModelState.IsValid);
            Assert.Contains("An error occurred while creating the AddOn.", _controller.ModelState[string.Empty].Errors[0].ErrorMessage);
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
            _cacheServiceMock.Setup(c => c.GetAsync<AddOnViewModel>(It.IsAny<string>())).ReturnsAsync((AddOnViewModel)null);
            _repositoryMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync((AddOn)null);

            // Act
            var result = await _controller.Edit(id);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task Edit_Get_CacheHit_ShouldReturnViewWithAddOn()
        {
            // Arrange
            int id = 1;
            var addonVM = new AddOnViewModel { Id = id, Name = "Cached", Price = 100 };
            _cacheServiceMock.Setup(c => c.GetAsync<AddOnViewModel>($"addon:id:{id}")).ReturnsAsync(addonVM);

            // Act
            var result = await _controller.Edit(id);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<AddOnViewModel>(viewResult.Model);
            Assert.Equal(id, model.Id);
            _repositoryMock.Verify(r => r.GetByIdAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task Edit_Get_CacheMiss_ShouldGetFromDbAndCache_AndReturnView()
        {
            // Arrange
            int id = 1;
            var addonEntity = new AddOn { Id = id, Name = "FromDb", Price = 50 };
            var expectedVM = (AddOnViewModel)addonEntity;

            _cacheServiceMock.Setup(c => c.GetAsync<AddOnViewModel>($"addon:id:{id}")).ReturnsAsync((AddOnViewModel)null);
            _repositoryMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(addonEntity);
            _cacheServiceMock.Setup(c => c.SetAsync($"addon:id:{id}", expectedVM, It.IsAny<TimeSpan>())).Returns(Task.CompletedTask);

            // Act
            var result = await _controller.Edit(id);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<AddOnViewModel>(viewResult.Model);
            Assert.Equal(id, model.Id);
            _repositoryMock.Verify(r => r.GetByIdAsync(id), Times.Once);
            _cacheServiceMock.Verify(c => c.SetAsync(
               $"addon:id:{id}",
               It.Is<AddOnViewModel>(vm => vm.Id == id && vm.Name == "FromDb" && vm.Price == 50),
               It.IsAny<TimeSpan>()),
               Times.Once);
        }

        [Fact]
        public async Task Edit_Get_Exception_ShouldRedirectToIndexWithError()
        {
            // Arrange
            int id = 1;
            _cacheServiceMock.Setup(c => c.GetAsync<AddOnViewModel>(It.IsAny<string>())).ThrowsAsync(new Exception("Cache error"));

            // Act
            var result = await _controller.Edit(id);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(nameof(AddOnController.Index), redirectResult.ActionName);
            Assert.Equal("An error occurred while retrieving the AddOn.", _controller.TempData["error"]);
        }

        #endregion

        #region Edit (POST)

        [Fact]
        public async Task Edit_Post_IdMismatch_ShouldReturnBadRequest()
        {
            // Arrange
            int routeId = 1;
            var addonVM = new AddOnViewModel { Id = 2, Name = "Test", Price = 10 };

            // Act
            var result = await _controller.Edit(routeId, addonVM);

            // Assert
            Assert.IsType<BadRequestResult>(result);
        }

        [Fact]
        public async Task Edit_Post_InvalidModel_ShouldReturnView()
        {
            // Arrange
            int id = 1;
            var addonVM = new AddOnViewModel { Id = id, Name = "Test", Price = 10 };
            _controller.ModelState.AddModelError("Name", "Required");

            // Act
            var result = await _controller.Edit(id, addonVM);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal(addonVM, viewResult.Model);
            _unitOfWorkMock.Verify(u => u.CompleteAsync(), Times.Never);
        }

        [Fact]
        public async Task Edit_Post_ValidModel_ShouldUpdateAndRedirect_AndInvalidateCache()
        {
            // Arrange
            int id = 1;
            var addonVM = new AddOnViewModel { Id = id, Name = "Updated", Price = 30 };
            var existingAddon = new AddOn { Id = id, Name = "Old Name", Price = 10 };

            _unitOfWorkMock.Setup(u => u.Repository<AddOn>().GetByIdAsync(id))
                .ReturnsAsync(existingAddon);        
            _unitOfWorkMock.Setup(u => u.CompleteAsync()).ReturnsAsync(1);
            _cacheServiceMock.Setup(c => c.RemoveAsync("addons:all")).Returns(Task.CompletedTask);
            _cacheServiceMock.Setup(c => c.RemoveAsync($"addon:id:{id}")).Returns(Task.CompletedTask);
            // Act
            var result = await _controller.Edit(id, addonVM);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(nameof(AddOnController.Index), redirectResult.ActionName);
            Assert.Equal("AddOn Updated Successfully", _controller.TempData["success"]);
            _repositoryMock.Verify(r => r.Update(It.Is<AddOn>(a => a.Id == id && a.Name == "Updated" && a.Price == 30)), Times.Once);
            _unitOfWorkMock.Verify(u => u.CompleteAsync(), Times.Once);
            _cacheServiceMock.Verify(c => c.RemoveAsync("addons:all"), Times.Once);
            _cacheServiceMock.Verify(c => c.RemoveAsync($"addon:id:{id}"), Times.Once);
        }

        [Fact]
        public async Task Edit_Post_Exception_ShouldReturnViewWithModelError_InDevelopment()
        {
            // Arrange
            int id = 1;
            var addonVM = new AddOnViewModel { Id = id, Name = "Test", Price = 10 };
            var exception = new Exception("Update failed");
            var existingAddon = new AddOn { Id = id, Name = "Old Name", Price = 5 };

            _envMock.Setup(e => e.EnvironmentName).Returns("Development");
            _unitOfWorkMock.Setup(u => u.Repository<AddOn>().GetByIdAsync(id))
               .ReturnsAsync(existingAddon);
            _unitOfWorkMock.Setup(u => u.CompleteAsync()).ThrowsAsync(exception);

            // Act
            var result = await _controller.Edit(id, addonVM);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal(addonVM, viewResult.Model);
            Assert.False(_controller.ModelState.IsValid);
            Assert.Contains("Update failed", _controller.ModelState[string.Empty].Errors[0].ErrorMessage);
        }

        [Fact]
        public async Task Edit_Post_Exception_ShouldReturnViewWithGenericError_InProduction()
        {
            // Arrange
            int id = 1;
            var addonVM = new AddOnViewModel { Id = id, Name = "Test", Price = 10 };
            var exception = new Exception("Update failed");
            var existingAddon = new AddOn { Id = id, Name = "Old Name", Price = 5 };

            _envMock.Setup(e => e.EnvironmentName).Returns("Production");
            _unitOfWorkMock.Setup(u => u.Repository<AddOn>().GetByIdAsync(id))
               .ReturnsAsync(existingAddon);
            _unitOfWorkMock.Setup(u => u.CompleteAsync()).ThrowsAsync(exception);

            // Act
            var result = await _controller.Edit(id, addonVM);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal(addonVM, viewResult.Model);
            Assert.False(_controller.ModelState.IsValid);
            Assert.Contains("An Error Has Occurred during Updating the AddOn", _controller.ModelState[string.Empty].Errors[0].ErrorMessage);
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
            _repositoryMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync((AddOn)null);

            // Act
            var result = await _controller.Delete(id);

            // Assert
            var jsonResult = Assert.IsType<JsonResult>(result);
            var jsonString = JsonSerializer.Serialize(jsonResult.Value);
            using var jsonDoc = JsonDocument.Parse(jsonString);
            var root = jsonDoc.RootElement;

            Assert.False(root.GetProperty("success").GetBoolean());
            Assert.Equal("Add-On not found", root.GetProperty("message").GetString());
        }

        [Fact]
        public async Task Delete_Valid_ShouldDeleteAndReturnSuccess_AndInvalidateCache()
        {
            // Arrange
            int id = 1;
            var addonEntity = new AddOn { Id = id, Name = "Test" };
            _repositoryMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(addonEntity);
            _unitOfWorkMock.Setup(u => u.CompleteAsync()).ReturnsAsync(1);
            _cacheServiceMock.Setup(c => c.RemoveAsync("addons:all")).Returns(Task.CompletedTask);
            _cacheServiceMock.Setup(c => c.RemoveAsync($"addon:id:{id}")).Returns(Task.CompletedTask);

            // Act
            var result = await _controller.Delete(id);
        
            // Assert
            var jsonResult = Assert.IsType<JsonResult>(result);
            var jsonString = JsonSerializer.Serialize(jsonResult.Value);
            using var jsonDoc = JsonDocument.Parse(jsonString);
            var root = jsonDoc.RootElement;

            Assert.True(root.GetProperty("success").GetBoolean());
            Assert.Equal("Delete Successful", root.GetProperty("message").GetString());

            _repositoryMock.Verify(r => r.Delete(addonEntity), Times.Once);
            _unitOfWorkMock.Verify(u => u.CompleteAsync(), Times.Once);
            _cacheServiceMock.Verify(c => c.RemoveAsync("addons:all"), Times.Once);
            _cacheServiceMock.Verify(c => c.RemoveAsync($"addon:id:{id}"), Times.Once);
        }

        [Fact]
        public async Task Delete_Exception_ShouldReturnJsonError()
        {
            // Arrange
            int id = 1;
            var addonEntity = new AddOn { Id = id, Name = "Test" };
            _repositoryMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(addonEntity);
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
