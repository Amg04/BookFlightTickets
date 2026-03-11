using BookFlightTickets.Core.Domain.Entities;
using BookFlightTickets.Core.Domain.RepositoryContracts;
using BookFlightTickets.Core.ViewModels;
using BookFlightTickets.UI.Areas.Admin.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging;
using Moq;

namespace BookFlightTickets.ControllerTests.Admin
{
    public class TicketAddOnsControllerTests
    {
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly Mock<IGenericRepository<TicketAddOns>> _ticketAddOnsRepoMock;
        private readonly Mock<ILogger<TicketAddOnsController>> _loggerMock;
        private readonly TicketAddOnsController _controller;
        private readonly ITempDataDictionary _tempData;

        public TicketAddOnsControllerTests()
        {
            _unitOfWorkMock = new Mock<IUnitOfWork>();
            _ticketAddOnsRepoMock = new Mock<IGenericRepository<TicketAddOns>>();
            _loggerMock = new Mock<ILogger<TicketAddOnsController>>();

            // Setup UnitOfWork to return mocked repository
            _unitOfWorkMock.Setup(u => u.Repository<TicketAddOns>()).Returns(_ticketAddOnsRepoMock.Object);

            // Create controller instance
            _controller = new TicketAddOnsController(
                _unitOfWorkMock.Object,
                _loggerMock.Object
            );

            // Setup TempData (if needed, though controller doesn't use it currently)
            _tempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>());
            _controller.TempData = _tempData;
        }

        #region Index

        [Fact]
        public async Task Index_ShouldReturnViewWithAddOns_WhenAddOnsExist()
        {
            // Arrange
            var addOnEntities = new List<TicketAddOns>
            {
                new TicketAddOns { Id = 1, TicketId = 1 , AddOnID = 1 , Ticket = new Ticket(),AddOn = new AddOn() },
                new TicketAddOns { Id = 2, TicketId = 1 , AddOnID = 1 , Ticket = new Ticket(),AddOn = new AddOn() },
            };

            _ticketAddOnsRepoMock.Setup(r => r.GetAllAsync())
                .ReturnsAsync(addOnEntities);

            // Act
            var result = await _controller.Index();

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<IEnumerable<TicketAddOnViewModel>>(viewResult.Model);
            Assert.Equal(2, model.Count());
            Assert.Null(_controller.ViewBag.InfoMessage);
            Assert.Null(_controller.ViewBag.ErrorMessage);
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Returning 2 ticket add-ons")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task Index_ShouldReturnViewWithInfoMessage_WhenDatabaseReturnsNull()
        {
            // Arrange
            _ticketAddOnsRepoMock.Setup(r => r.GetAllAsync())
                .ReturnsAsync((IEnumerable<TicketAddOns>)null);

            // Act
            var result = await _controller.Index();

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<IEnumerable<TicketAddOnViewModel>>(viewResult.Model);
            Assert.Empty(model);
            Assert.Equal("No ticket add-ons available.", _controller.ViewBag.InfoMessage);
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Database returned null for ticket add-ons")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task Index_ShouldReturnViewWithInfoMessage_WhenAddOnsListIsEmpty()
        {
            // Arrange
            var addOnEntities = new List<TicketAddOns>(); // empty list

            _ticketAddOnsRepoMock.Setup(r => r.GetAllAsync())
                .ReturnsAsync(addOnEntities);

            // Act
            var result = await _controller.Index();

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<IEnumerable<TicketAddOnViewModel>>(viewResult.Model);
            Assert.Empty(model);
            Assert.Equal("No ticket AddOns available.", _controller.ViewBag.InfoMessage);
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("No ticket AddOns found in database")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task Index_ShouldReturnViewWithErrorMessage_WhenExceptionOccurs()
        {
            // Arrange
            _ticketAddOnsRepoMock.Setup(r => r.GetAllAsync())
                .ThrowsAsync(new Exception("Database connection failed"));

            // Act
            var result = await _controller.Index();

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<IEnumerable<TicketAddOnViewModel>>(viewResult.Model);
            Assert.Empty(model);
            Assert.Equal("An error occurred while retrieving ticket add-ons.", _controller.ViewBag.ErrorMessage);
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Error occurred while retrieving ticket add-ons in Index action")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        #endregion
    }
}