using BookFlightTickets.Core.Domain.Entities;
using BookFlightTickets.Core.Domain.RepositoryContracts;
using BookFlightTickets.Core.Domain.Specifications;
using BookFlightTickets.Core.Shared.Utility;
using BookFlightTickets.UI.Areas.Customer.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging;
using Moq;
using Rotativa.AspNetCore;
using System.Security.Claims;

namespace BookFlightTickets.ControllerTests.Customer
{
    public class MyBookingsControllerTests
    {
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly Mock<IGenericRepository<Booking>> _bookingRepoMock;
        private readonly Mock<ILogger<BookController>> _loggerMock; // as per controller's logger type
        private readonly MyBookingsController _controller;
        private readonly ITempDataDictionary _tempData;
        private readonly string _userId = "test-user-id";

        public MyBookingsControllerTests()
        {
            _unitOfWorkMock = new Mock<IUnitOfWork>();
            _bookingRepoMock = new Mock<IGenericRepository<Booking>>();
            _loggerMock = new Mock<ILogger<BookController>>();

            _unitOfWorkMock.Setup(u => u.Repository<Booking>()).Returns(_bookingRepoMock.Object);

            _controller = new MyBookingsController(
                _unitOfWorkMock.Object,
                _loggerMock.Object
            );

            // Setup authenticated user
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, _userId)
            };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            var user = new ClaimsPrincipal(identity);

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            };

            // Setup TempData
            _tempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>());
            _controller.TempData = _tempData;
        }

        #region Index

        [Fact]
        public async Task Index_UserAuthenticated_ReturnsViewWithBookings()
        {
            // Arrange
            var bookings = new List<Booking>
            {
                new Booking { Id = 1, UserId = _userId, PNR = "ABC123" },
                new Booking { Id = 2, UserId = _userId, PNR = "DEF456" }
            };

            _bookingRepoMock.Setup(r => r.GetAllWithSpecAsync(
                    It.IsAny<ISpecification<Booking>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(bookings);

            // Act
            var result = await _controller.Index();

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<IEnumerable<Booking>>(viewResult.Model);
            Assert.Equal(2, model.Count());
            _bookingRepoMock.Verify(r => r.GetAllWithSpecAsync(
                It.IsAny<ISpecification<Booking>>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Index_UserNotAuthenticated_ReturnsUnauthorized()
        {
            // Arrange
            _controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(); // empty

            // Act
            var result = await _controller.Index();

            // Assert
            var unauthorizedResult = Assert.IsType<UnauthorizedResult>(result);
            _bookingRepoMock.Verify(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<Booking>>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Index_Exception_ReturnsViewWithErrorMessageAndEmptyList()
        {
            // Arrange
            _bookingRepoMock.Setup(r => r.GetAllWithSpecAsync(
                    It.IsAny<ISpecification<Booking>>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await _controller.Index();

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<IEnumerable<Booking>>(viewResult.Model);
            Assert.Empty(model);
            Assert.Equal("An error occurred while loading your bookings. Please try again.", _controller.ViewBag.ErrorMessage);
        }

        #endregion

        #region BookingPDF

        [Fact]
        public async Task BookingPDF_UserAuthenticatedAndBookingExists_ReturnsViewAsPdf()
        {
            // Arrange
            int bookingId = 1;
            var booking = new Booking
            {
                Id = bookingId,
                UserId = _userId,
                PNR = "ABC123",
                Tickets = new List<Ticket>(),
                Flight = new Flight()
            };

            _bookingRepoMock.Setup(r => r.GetEntityWithSpecAsync(
                    It.IsAny<ISpecification<Booking>>()))
                .ReturnsAsync(booking);

            // Act
            var result = await _controller.BookingPDF(bookingId);

            // Assert
            var pdfResult = Assert.IsType<ViewAsPdf>(result);
            Assert.Equal($"Booking_{booking.PNR}.pdf", pdfResult.FileName);
            Assert.Equal(booking, pdfResult.Model);
            _bookingRepoMock.Verify(r => r.GetEntityWithSpecAsync(
                It.IsAny<ISpecification<Booking>>()), Times.Once);
        }

        [Fact]
        public async Task BookingPDF_UserNotAuthenticated_ReturnsUnauthorized()
        {
            // Arrange
            _controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(); // empty

            // Act
            var result = await _controller.BookingPDF(1);

            // Assert
            var unauthorizedResult = Assert.IsType<UnauthorizedResult>(result);
            _bookingRepoMock.Verify(r => r.GetEntityWithSpecAsync(It.IsAny<ISpecification<Booking>>()), Times.Never);
        }

        [Fact]
        public async Task BookingPDF_BookingNotFound_ReturnsNotFound()
        {
            // Arrange
            int bookingId = 99;
            _bookingRepoMock.Setup(r => r.GetEntityWithSpecAsync(
                    It.IsAny<ISpecification<Booking>>()))
                .ReturnsAsync((Booking)null);

            // Act
            var result = await _controller.BookingPDF(bookingId);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task BookingPDF_Exception_RedirectsToIndexWithError()
        {
            // Arrange
            int bookingId = 1;
            _bookingRepoMock.Setup(r => r.GetEntityWithSpecAsync(
                    It.IsAny<ISpecification<Booking>>() ))
                .ThrowsAsync(new Exception("PDF generation error"));

            // Act
            var result = await _controller.BookingPDF(bookingId);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(nameof(MyBookingsController.Index), redirectResult.ActionName);
            Assert.Equal("An error occurred while generating the booking PDF.", _controller.TempData["error"]);
        }

        #endregion
    }
}