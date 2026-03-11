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
    public class TicketsControllerTests
    {
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly Mock<IGenericRepository<Ticket>> _ticketRepoMock;
        private readonly Mock<ILogger<TicketsController>> _loggerMock;
        private readonly TicketsController _controller;
        private readonly ITempDataDictionary _tempData;

        public TicketsControllerTests()
        {
            _unitOfWorkMock = new Mock<IUnitOfWork>();
            _ticketRepoMock = new Mock<IGenericRepository<Ticket>>();
            _loggerMock = new Mock<ILogger<TicketsController>>();

            // Setup UnitOfWork to return mocked repository
            _unitOfWorkMock.Setup(u => u.Repository<Ticket>()).Returns(_ticketRepoMock.Object);

            // Create controller instance
            _controller = new TicketsController(
                _unitOfWorkMock.Object,
                _loggerMock.Object
            );

            // Setup TempData (if needed, though controller doesn't use it currently)
            _tempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>());
            _controller.TempData = _tempData;
        }

        #region Index

        [Fact]
        public async Task Index_ShouldReturnViewWithTickets_WhenTicketsExist()
        {
            // Arrange
            var ticketEntities = new List<Ticket>
            {
                new Ticket
                {
                    Id = 1,
                    FlightSeat = new FlightSeat
                    {
                        Seat = new Seat { Row = "A", Number = 1 }
                    }
                },
                new Ticket
                {
                    Id = 2,
                    FlightSeat = new FlightSeat
                    {
                        Seat = new Seat { Row = "B", Number = 2 }
                    }
                }
            };

            _ticketRepoMock.Setup(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<Ticket>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(ticketEntities);

            // Act
            var result = await _controller.Index();

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<IEnumerable<TicketViewModel>>(viewResult.Model);
            Assert.Equal(2, model.Count());
            Assert.Null(_controller.ViewBag.InfoMessage);
            Assert.Null(_controller.ViewBag.ErrorMessage);

            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Returning 2 tickets")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task Index_ShouldReturnViewWithInfoMessage_WhenTicketsNull()
        {
            // Arrange
            _ticketRepoMock.Setup(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<Ticket>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IEnumerable<Ticket>)null);

            // Act
            var result = await _controller.Index();

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<IEnumerable<TicketViewModel>>(viewResult.Model);
            Assert.Empty(model);
            Assert.Equal("No tickets available.", _controller.ViewBag.InfoMessage);

            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("No tickets found in database")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task Index_ShouldReturnViewWithInfoMessage_WhenTicketsEmpty()
        {
            // Arrange
            var ticketEntities = new List<Ticket>(); // empty list

            _ticketRepoMock.Setup(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<Ticket>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(ticketEntities);

            // Act
            var result = await _controller.Index();

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<IEnumerable<TicketViewModel>>(viewResult.Model);
            Assert.Empty(model);
            Assert.Equal("No tickets available.", _controller.ViewBag.InfoMessage);

            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("No tickets found in database")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task Index_ShouldReturnViewWithErrorMessage_WhenExceptionOccurs()
        {
            // Arrange
            _ticketRepoMock.Setup(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<Ticket>>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Database connection failed"));

            // Act
            var result = await _controller.Index();

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<IEnumerable<TicketViewModel>>(viewResult.Model);
            Assert.Empty(model);
            Assert.Equal("An error occurred while retrieving tickets.", _controller.ViewBag.ErrorMessage);

            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Error occurred while retrieving tickets in Index action")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        #endregion
    }
}