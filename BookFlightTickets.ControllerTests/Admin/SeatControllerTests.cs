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
    public class SeatControllerTests
    {
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly Mock<IGenericRepository<Seat>> _seatRepoMock;
        private readonly Mock<IGenericRepository<Airplane>> _airplaneRepoMock;
        private readonly Mock<IRedisCacheService> _cacheServiceMock;
        private readonly Mock<IWebHostEnvironment> _envMock;
        private readonly Mock<ILogger<SeatController>> _loggerMock;
        private readonly SeatController _controller;
        private readonly ITempDataDictionary _tempData;

        public SeatControllerTests()
        {
            _unitOfWorkMock = new Mock<IUnitOfWork>();
            _seatRepoMock = new Mock<IGenericRepository<Seat>>();
            _airplaneRepoMock = new Mock<IGenericRepository<Airplane>>();
            _cacheServiceMock = new Mock<IRedisCacheService>();
            _envMock = new Mock<IWebHostEnvironment>();
            _loggerMock = new Mock<ILogger<SeatController>>();

            // Setup UnitOfWork to return mocked repositories
            _unitOfWorkMock.Setup(u => u.Repository<Seat>()).Returns(_seatRepoMock.Object);
            _unitOfWorkMock.Setup(u => u.Repository<Airplane>()).Returns(_airplaneRepoMock.Object);

            // Create controller instance
            _controller = new SeatController(
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
        public async Task Index_ShouldReturnViewWithSeats_WhenCacheHit()
        {
            // Arrange
            var seats = new List<SeatViewModel>
            {
                new SeatViewModel { Id = 1 },
                new SeatViewModel { Id = 2 }
            };

            _cacheServiceMock.Setup(c => c.GetOrSetAsync(
                    It.Is<string>(key => key == "seats:all"),
                    It.IsAny<Func<Task<List<SeatViewModel>>>>(),
                    It.IsAny<TimeSpan>()))
                .ReturnsAsync(seats);

            // Act
            var result = await _controller.Index();

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<IEnumerable<SeatViewModel>>(viewResult.Model);
            Assert.Equal(2, model.Count());
            _cacheServiceMock.Verify(c => c.GetOrSetAsync("seats:all", It.IsAny<Func<Task<List<SeatViewModel>>>>(), It.IsAny<TimeSpan>()), Times.Once);
        }

        [Fact]
        public async Task Index_ShouldReturnViewWithSeats_WhenCacheMiss()
        {
            // Arrange
            var seatEntities = new List<Seat>
            {
                new Seat { Id = 1, Airplane = new Airplane() },
                new Seat { Id = 2, Airplane = new Airplane() }
            };

            _unitOfWorkMock.Setup(u => u.Repository<Seat>()).Returns(_seatRepoMock.Object);

            _seatRepoMock.Setup(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<Seat>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(seatEntities);

            _cacheServiceMock
                .Setup(c => c.GetOrSetAsync(
                    It.Is<string>(key => key == "seats:all"),
                    It.IsAny<Func<Task<List<SeatViewModel>>>>(),
                    It.IsAny<TimeSpan>()))
                .Returns((string key, Func<Task<List<SeatViewModel>>> factory, TimeSpan expiry) => factory());


            // Act
            var result = await _controller.Index();

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<IEnumerable<SeatViewModel>>(viewResult.Model);
            Assert.Equal(2, model.Count());
            _seatRepoMock.Verify(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<Seat>>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Index_ShouldReturnViewWithInfoMessage_WhenNoSeats()
        {
            // Arrange
            _cacheServiceMock.Setup(c => c.GetOrSetAsync(
                    It.IsAny<string>(),
                    It.IsAny<Func<Task<List<SeatViewModel>>>>(),
                    It.IsAny<TimeSpan>()))
                .ReturnsAsync(new List<SeatViewModel>());

            // Act
            var result = await _controller.Index();

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<IEnumerable<SeatViewModel>>(viewResult.Model);
            Assert.Empty(model);
            Assert.Equal("No seats available.", _controller.ViewBag.InfoMessage);
        }

        [Fact]
        public async Task Index_ShouldReturnViewWithError_WhenExceptionOccurs()
        {
            // Arrange
            _cacheServiceMock.Setup(c => c.GetOrSetAsync(
                    It.IsAny<string>(),
                    It.IsAny<Func<Task<List<SeatViewModel>>>>(),
                    It.IsAny<TimeSpan>()))
                .ThrowsAsync(new Exception("Test exception"));

            // Act
            var result = await _controller.Index();

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<IEnumerable<SeatViewModel>>(viewResult.Model);
            Assert.Empty(model);
            Assert.Equal("An error occurred while retrieving seats.", _controller.ViewBag.ErrorMessage);
        }

        #endregion

        #region Create (GET)

        [Fact]
        public async Task Create_Get_ShouldReturnViewWithDropDowns()
        {
            // Arrange
            var airplanes = new List<Airplane> { new Airplane { Id = 1, Model = "B737" } };
            _airplaneRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(airplanes);

            // Act
            var result = await _controller.Create();

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.NotNull(_controller.ViewBag.Airplanes);
            Assert.NotNull(_controller.ViewBag.SeatClasses);
        }

        [Fact]
        public async Task Create_Get_WhenException_ShouldRedirectToIndexWithError()
        {
            // Arrange
            _airplaneRepoMock.Setup(r => r.GetAllAsync()).ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await _controller.Create();

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(nameof(SeatController.Index), redirectResult.ActionName);
            Assert.Equal("An error occurred while loading the form.", _controller.TempData["error"]);
        }

        #endregion

        #region Create (POST)

        [Fact]
        public async Task Create_Post_InvalidModel_ShouldReturnViewWithModelAndDropDowns()
        {
            // Arrange
            var seatVM = new SeatViewModel { AirplaneId = 1 };
            _controller.ModelState.AddModelError("Row", "Required");

            var airplanes = new List<Airplane> { new Airplane { Id = 1, Model = "B737" } };
            _airplaneRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(airplanes);

            // Act
            var result = await _controller.Create(seatVM);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal(seatVM, viewResult.Model);
            Assert.NotNull(_controller.ViewBag.Airplanes);
            Assert.NotNull(_controller.ViewBag.SeatClasses);
            _seatRepoMock.Verify(r => r.AddAsync(It.IsAny<Seat>()), Times.Never);
        }

        [Fact]
        public async Task Create_Post_AirplaneNotFound_ShouldReturnNotFound()
        {
            // Arrange
            var seatVM = new SeatViewModel { AirplaneId = 99 };
            _airplaneRepoMock.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((Airplane)null);

            // Act
            var result = await _controller.Create(seatVM);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task Create_Post_SeatCapacityFull_ShouldRedirectToIndexWithError()
        {
            // Arrange
            var seatVM = new SeatViewModel { AirplaneId = 1 };
            var airplane = new Airplane { Id = 1, SeatCapacity = 5 };
            _airplaneRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(airplane);
            _seatRepoMock.Setup(r => r.CountAsync(It.IsAny<ISpecification<Seat>>()))
                .ReturnsAsync(5);

            // Act
            var result = await _controller.Create(seatVM);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(nameof(SeatController.Index), redirectResult.ActionName);
            Assert.Equal("The seat capacity is full. You cannot add more seats.", _controller.TempData["error"]);
            _seatRepoMock.Verify(r => r.AddAsync(It.IsAny<Seat>()), Times.Never);
        }

        [Fact]
        public async Task Create_Post_ValidModel_ShouldAddSeatAndInvalidateCache_AndRedirectToIndex()
        {
            // Arrange
            var seatVM = new SeatViewModel
            {
                Id = 0,
                AirplaneId = 1,
                Row = "A",
                Number = 1,
                Class = SeatClass.Economy,
                Price = 100
            };

            var airplane = new Airplane { Id = 1, SeatCapacity = 10 };
            _airplaneRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(airplane);
            _seatRepoMock.Setup(r => r.CountAsync(It.IsAny<ISpecification<Seat>>()))
                .ReturnsAsync(3); // under capacity

            Seat capturedSeat = null;
            _seatRepoMock.Setup(r => r.AddAsync(It.IsAny<Seat>()))
                .Callback<Seat>(s => capturedSeat = s)
                .Returns(Task.CompletedTask);

            _unitOfWorkMock.Setup(u => u.CompleteAsync()).ReturnsAsync(1);

            // Cache invalidation
            _cacheServiceMock.Setup(c => c.RemoveAsync("seats:all")).Returns(Task.CompletedTask);
            _cacheServiceMock.Setup(c => c.RemoveByPatternAsync("seat:id:*")).Returns(Task.CompletedTask);
            _cacheServiceMock.Setup(c => c.RemoveByPatternAsync("seat:details:id:*")).Returns(Task.CompletedTask);

            // Act
            var result = await _controller.Create(seatVM);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(nameof(SeatController.Index), redirectResult.ActionName);
            Assert.Equal("Seat has been Added Successfully", _controller.TempData["success"]);

            Assert.NotNull(capturedSeat);
            Assert.Equal(seatVM.AirplaneId, capturedSeat.AirplaneId);
            Assert.Equal(seatVM.Row, capturedSeat.Row);
            Assert.Equal(seatVM.Number, capturedSeat.Number);
            Assert.Equal(seatVM.Class, capturedSeat.Class);
            Assert.Equal(seatVM.Price, capturedSeat.Price);

            _seatRepoMock.Verify(r => r.AddAsync(It.IsAny<Seat>()), Times.Once);
            _unitOfWorkMock.Verify(u => u.CompleteAsync(), Times.Once);
            _cacheServiceMock.Verify(c => c.RemoveAsync("seats:all"), Times.Once);
            _cacheServiceMock.Verify(c => c.RemoveByPatternAsync("seat:id:*"), Times.Once);
            _cacheServiceMock.Verify(c => c.RemoveByPatternAsync("seat:details:id:*"), Times.Once);
        }

        [Fact]
        public async Task Create_Post_WhenSaveFails_ShouldReturnViewWithModelError()
        {
            // Arrange
            var seatVM = new SeatViewModel { AirplaneId = 1 };
            var airplane = new Airplane { Id = 1, SeatCapacity = 10 };
            _airplaneRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(airplane);
            _seatRepoMock.Setup(r => r.CountAsync(It.IsAny<ISpecification<Seat>>()))
                .ReturnsAsync(3);
            _unitOfWorkMock.Setup(u => u.CompleteAsync()).ReturnsAsync(0);

            // Act
            var result = await _controller.Create(seatVM);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal(seatVM, viewResult.Model);
            Assert.False(_controller.ModelState.IsValid);
            Assert.Contains("Failed to save Seat.", _controller.ModelState[string.Empty].Errors[0].ErrorMessage);
        }

        [Fact]
        public async Task Create_Post_Exception_ShouldReturnViewWithModelError()
        {
            // Arrange
            var seatVM = new SeatViewModel { AirplaneId = 1 };
            var airplane = new Airplane { Id = 1, SeatCapacity = 10 };
            _airplaneRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(airplane);
            _seatRepoMock.Setup(r => r.CountAsync(It.IsAny<ISpecification<Seat>>()))
                .ReturnsAsync(3);
            _seatRepoMock.Setup(r => r.AddAsync(It.IsAny<Seat>()))
                .ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await _controller.Create(seatVM);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal(seatVM, viewResult.Model);
            Assert.False(_controller.ModelState.IsValid);
            Assert.Contains("An error occurred while creating the Seat.", _controller.ModelState[string.Empty].Errors[0].ErrorMessage);
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
            _cacheServiceMock.Setup(c => c.GetAsync<SeatViewModel>(It.IsAny<string>())).ReturnsAsync((SeatViewModel)null);
            _seatRepoMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync((Seat)null);

            // Act
            var result = await _controller.Edit(id);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task Edit_Get_CacheHit_ShouldReturnViewWithSeatAndDropDowns()
        {
            // Arrange
            int id = 1;
            var seatVM = new SeatViewModel { Id = id, AirplaneId = 2 };
            _cacheServiceMock.Setup(c => c.GetAsync<SeatViewModel>($"seat:id:{id}")).ReturnsAsync(seatVM);

            var airplanes = new List<Airplane> { new Airplane { Id = 2, Model = "B737" } };
            _airplaneRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(airplanes);

            // Act
            var result = await _controller.Edit(id);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<SeatViewModel>(viewResult.Model);
            Assert.Equal(id, model.Id);
            Assert.NotNull(_controller.ViewBag.Airplanes);
            Assert.NotNull(_controller.ViewBag.SeatClasses);
            _seatRepoMock.Verify(r => r.GetByIdAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task Edit_Get_CacheMiss_ShouldGetFromDbAndCache_AndReturnView()
        {
            // Arrange
            int id = 1;
            var seatEntity = new Seat { Id = id, AirplaneId = 2 };
            var expectedVM = (SeatViewModel)seatEntity;

            _cacheServiceMock.Setup(c => c.GetAsync<SeatViewModel>($"seat:id:{id}")).ReturnsAsync((SeatViewModel)null);
            _seatRepoMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(seatEntity);
            _cacheServiceMock.Setup(c => c.SetAsync($"seat:id:{id}", expectedVM, It.IsAny<TimeSpan>())).Returns(Task.CompletedTask);

            var airplanes = new List<Airplane> { new Airplane { Id = 2, Model = "B737" } };
            _airplaneRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(airplanes);

            // Act
            var result = await _controller.Edit(id);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<SeatViewModel>(viewResult.Model);
            Assert.Equal(id, model.Id);
            _seatRepoMock.Verify(r => r.GetByIdAsync(id), Times.Once);
            _cacheServiceMock.Verify(c => c.SetAsync($"seat:id:{id}", It.IsAny<SeatViewModel>(), It.IsAny<TimeSpan>()), Times.Once);
        }

        [Fact]
        public async Task Edit_Get_Exception_ShouldRedirectToIndexWithError()
        {
            // Arrange
            int id = 1;
            _cacheServiceMock.Setup(c => c.GetAsync<SeatViewModel>(It.IsAny<string>())).ThrowsAsync(new Exception("Cache error"));

            // Act
            var result = await _controller.Edit(id);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(nameof(SeatController.Index), redirectResult.ActionName);
            Assert.Equal("An error occurred while retrieving the Seat.", _controller.TempData["error"]);
        }

        #endregion

        #region Edit (POST)

        [Fact]
        public async Task Edit_Post_IdMismatch_ShouldReturnNotFound()
        {
            // Arrange
            int routeId = 1;
            var seatVM = new SeatViewModel { Id = 2 };

            // Act
            var result = await _controller.Edit(routeId, seatVM);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task Edit_Post_InvalidModel_ShouldReturnViewWithModelAndDropDowns()
        {
            // Arrange
            int id = 1;
            var seatVM = new SeatViewModel { Id = id, AirplaneId = 2 };
            _controller.ModelState.AddModelError("Row", "Required");

            var airplanes = new List<Airplane> { new Airplane { Id = 2, Model = "B737" } };
            _airplaneRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(airplanes);

            // Act
            var result = await _controller.Edit(id, seatVM);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal(seatVM, viewResult.Model);
            Assert.NotNull(_controller.ViewBag.Airplanes);
            Assert.NotNull(_controller.ViewBag.SeatClasses);
            _seatRepoMock.Verify(r => r.Update(It.IsAny<Seat>()), Times.Never);
        }

        [Fact]
        public async Task Edit_Post_ValidModel_ShouldUpdateAndInvalidateCache_AndRedirectToIndex()
        {
            // Arrange
            int id = 1;
            var seatVM = new SeatViewModel
            {
                Id = id,
                AirplaneId = 2,
                Row = "A",
                Number = 1,
                Class = SeatClass.Business,
                Price = 200
            };
            var existingSeat = new Seat { Id = id };
            var seatRepoMock = new Mock<IGenericRepository<Seat>>();
            seatRepoMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(existingSeat);
            seatRepoMock.Setup(r => r.Update(It.IsAny<Seat>())).Verifiable();
            _unitOfWorkMock.Setup(u => u.Repository<Seat>()).Returns(seatRepoMock.Object);

            _unitOfWorkMock.Setup(u => u.CompleteAsync()).ReturnsAsync(1);

            _cacheServiceMock.Setup(c => c.RemoveAsync("seats:all")).Returns(Task.CompletedTask);
            _cacheServiceMock.Setup(c => c.RemoveAsync($"seat:id:{id}")).Returns(Task.CompletedTask);
            _cacheServiceMock.Setup(c => c.RemoveAsync($"seat:details:id:{id}")).Returns(Task.CompletedTask);

            // Act
            var result = await _controller.Edit(id, seatVM);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(nameof(SeatController.Index), redirectResult.ActionName);
            Assert.Equal("Seat Updated Successfully", _controller.TempData["success"]);
            Assert.Equal(seatVM.AirplaneId, existingSeat.AirplaneId);
            Assert.Equal(seatVM.Row, existingSeat.Row);
            Assert.Equal(seatVM.Number, existingSeat.Number);
            Assert.Equal(seatVM.Class, existingSeat.Class);
            Assert.Equal(seatVM.Price, existingSeat.Price);
            seatRepoMock.Verify(r => r.Update(existingSeat), Times.Once);
            _unitOfWorkMock.Verify(u => u.CompleteAsync(), Times.Once);
            _cacheServiceMock.Verify(c => c.RemoveAsync("seats:all"), Times.Once);
            _cacheServiceMock.Verify(c => c.RemoveAsync($"seat:id:{id}"), Times.Once);
            _cacheServiceMock.Verify(c => c.RemoveAsync($"seat:details:id:{id}"), Times.Once);
        }

        [Fact]
        public async Task Edit_Post_Exception_ShouldReturnViewWithModelError_InDevelopment()
        {
            // Arrange
            int id = 1;
            var seatVM = new SeatViewModel { Id = id };
            var existingSeat = new Seat { Id = id };
            var exception = new Exception("Update failed");

            var seatRepoMock = new Mock<IGenericRepository<Seat>>();
            seatRepoMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(existingSeat);
            _unitOfWorkMock.Setup(u => u.Repository<Seat>()).Returns(seatRepoMock.Object);
            _unitOfWorkMock.Setup(u => u.CompleteAsync()).ThrowsAsync(exception);

            var airplaneRepoMock = new Mock<IGenericRepository<Airplane>>();
            var airplanes = new List<Airplane> { new Airplane { Id = 2, Model = "B737" } };
            airplaneRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(airplanes);
            _unitOfWorkMock.Setup(u => u.Repository<Airplane>()).Returns(airplaneRepoMock.Object);

            _envMock.Setup(e => e.EnvironmentName).Returns("Development");          

            // Act
            var result = await _controller.Edit(id, seatVM);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal(seatVM, viewResult.Model);
            Assert.False(_controller.ModelState.IsValid);
            Assert.Contains("Update failed", _controller.ModelState[string.Empty].Errors[0].ErrorMessage);
            _cacheServiceMock.Verify(c => c.RemoveAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task Edit_Post_Exception_ShouldReturnViewWithGenericError_InProduction()
        {
            // Arrange
            int id = 1;
            var seatVM = new SeatViewModel { Id = id };
            var existingSeat = new Seat { Id = id };
            var exception = new Exception("Update failed");

            var seatRepoMock = new Mock<IGenericRepository<Seat>>();
            seatRepoMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(existingSeat);
            _unitOfWorkMock.Setup(u => u.Repository<Seat>()).Returns(seatRepoMock.Object);
            _unitOfWorkMock.Setup(u => u.CompleteAsync()).ThrowsAsync(exception);

            var airplaneRepoMock = new Mock<IGenericRepository<Airplane>>();
            var airplanes = new List<Airplane> { new Airplane { Id = 2, Model = "B737" } };
            airplaneRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(airplanes);
            _unitOfWorkMock.Setup(u => u.Repository<Airplane>()).Returns(airplaneRepoMock.Object);

            _envMock.Setup(e => e.EnvironmentName).Returns("Production");

            // Act
            var result = await _controller.Edit(id, seatVM);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal(seatVM, viewResult.Model);
            Assert.False(_controller.ModelState.IsValid);
            Assert.Contains("An Error Has Occurred during Updating the Seat", _controller.ModelState[string.Empty].Errors[0].ErrorMessage);
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
            _seatRepoMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync((Seat)null);

            // Act
            var result = await _controller.Delete(id);

            // Assert
            var jsonResult = Assert.IsType<JsonResult>(result);
            var jsonString = JsonSerializer.Serialize(jsonResult.Value);
            using var jsonDoc = JsonDocument.Parse(jsonString);
            var root = jsonDoc.RootElement;

            Assert.False(root.GetProperty("success").GetBoolean());
            Assert.Equal("Seat not found", root.GetProperty("message").GetString());
        }

        [Fact]
        public async Task Delete_Valid_ShouldDeleteAndInvalidateCache_AndReturnJsonSuccess()
        {
            // Arrange
            int id = 1;
            var seatEntity = new Seat { Id = id };
            _seatRepoMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(seatEntity);
            _unitOfWorkMock.Setup(u => u.CompleteAsync()).ReturnsAsync(1);

            _cacheServiceMock.Setup(c => c.RemoveAsync("seats:all")).Returns(Task.CompletedTask);
            _cacheServiceMock.Setup(c => c.RemoveAsync($"seat:id:{id}")).Returns(Task.CompletedTask);
            _cacheServiceMock.Setup(c => c.RemoveAsync($"seat:details:id:{id}")).Returns(Task.CompletedTask);

            // Act
            var result = await _controller.Delete(id);

            // Assert
            var jsonResult = Assert.IsType<JsonResult>(result);
            var jsonString = JsonSerializer.Serialize(jsonResult.Value);
            using var jsonDoc = JsonDocument.Parse(jsonString);
            var root = jsonDoc.RootElement;

            Assert.True(root.GetProperty("success").GetBoolean());
            Assert.Equal("Delete Successful", root.GetProperty("message").GetString());

            _seatRepoMock.Verify(r => r.Delete(seatEntity), Times.Once);
            _unitOfWorkMock.Verify(u => u.CompleteAsync(), Times.Once);
            _cacheServiceMock.Verify(c => c.RemoveAsync("seats:all"), Times.Once);
            _cacheServiceMock.Verify(c => c.RemoveAsync($"seat:id:{id}"), Times.Once);
            _cacheServiceMock.Verify(c => c.RemoveAsync($"seat:details:id:{id}"), Times.Once);
        }

        [Fact]
        public async Task Delete_Exception_ShouldReturnJsonError()
        {
            // Arrange
            int id = 1;
            var seatEntity = new Seat { Id = id };
            _seatRepoMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(seatEntity);
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
            _cacheServiceMock.Setup(c => c.GetAsync<SeatViewModel>(It.IsAny<string>())).ReturnsAsync((SeatViewModel)null);
            _seatRepoMock.Setup(r => r.GetEntityWithSpecAsync(It.IsAny<ISpecification<Seat>>()))
                .ReturnsAsync((Seat)null);

            // Act
            var result = await _controller.Details(id);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task Details_CacheHit_ShouldReturnViewWithSeat()
        {
            // Arrange
            int id = 1;
            var seatVM = new SeatViewModel { Id = id };
            _cacheServiceMock.Setup(c => c.GetAsync<SeatViewModel>($"seat:details:id:{id}")).ReturnsAsync(seatVM);

            // Act
            var result = await _controller.Details(id);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<SeatViewModel>(viewResult.Model);
            Assert.Equal(id, model.Id);
            _seatRepoMock.Verify(r => r.GetEntityWithSpecAsync(It.IsAny<ISpecification<Seat>>()), Times.Never);
        }

        [Fact]
        public async Task Details_CacheMiss_ShouldGetFromDbAndCacheBothKeys_AndReturnView()
        {
            // Arrange
            int id = 1;
            var seatEntity = new Seat { Id = id, Airplane = new Airplane() };
            var expectedVM = (SeatViewModel)seatEntity;

            _cacheServiceMock.Setup(c => c.GetAsync<SeatViewModel>($"seat:details:id:{id}")).ReturnsAsync((SeatViewModel)null);
            _seatRepoMock.Setup(r => r.GetEntityWithSpecAsync(It.IsAny<ISpecification<Seat>>()))
                .ReturnsAsync(seatEntity);
            _cacheServiceMock.Setup(c => c.SetAsync($"seat:details:id:{id}", expectedVM, It.IsAny<TimeSpan>())).Returns(Task.CompletedTask);
            _cacheServiceMock.Setup(c => c.SetAsync($"seat:id:{id}", expectedVM, It.IsAny<TimeSpan>())).Returns(Task.CompletedTask);

            // Act
            var result = await _controller.Details(id);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<SeatViewModel>(viewResult.Model);
            Assert.Equal(id, model.Id);
            _seatRepoMock.Verify(r => r.GetEntityWithSpecAsync(It.IsAny<ISpecification<Seat>>()), Times.Once);
            _cacheServiceMock.Verify(c => c.SetAsync($"seat:details:id:{id}", It.IsAny<SeatViewModel>(), It.IsAny<TimeSpan>()), Times.Once);
            _cacheServiceMock.Verify(c => c.SetAsync($"seat:id:{id}", It.IsAny<SeatViewModel>(), It.IsAny<TimeSpan>()), Times.Once);
        }

        [Fact]
        public async Task Details_Exception_ShouldRedirectToIndexWithError()
        {
            // Arrange
            int id = 1;
            _cacheServiceMock.Setup(c => c.GetAsync<SeatViewModel>(It.IsAny<string>())).ThrowsAsync(new Exception("Cache error"));

            // Act
            var result = await _controller.Details(id);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(nameof(SeatController.Index), redirectResult.ActionName);
            Assert.Equal("An error occurred while retrieving the Seat details.", _controller.TempData["error"]);
        }

        #endregion
    }
}