using BookFlightTickets.Core.Domain.Entities;
using BookFlightTickets.Core.Domain.RepositoryContracts;
using BookFlightTickets.Core.Domain.ResultPattern;
using BookFlightTickets.Core.Domain.Specifications;
using BookFlightTickets.Core.ServiceContracts;
using BookFlightTickets.Core.Shared.Utility;
using BookFlightTickets.Core.ViewModels;
using BookFlightTickets.UI.Areas.Customer.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;

namespace BookFlightTickets.ControllerTests.Customer
{
    public class BookControllerTests
    {
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly Mock<IBookingService> _bookingServiceMock;
        private readonly Mock<ILogger<BookController>> _loggerMock;
        private readonly Mock<IEmailSender> _emailSenderMock;
        private readonly Mock<IGenericRepository<FlightSeat>> _flightSeatRepoMock;
        private readonly Mock<IGenericRepository<AddOn>> _addOnRepoMock;
        private readonly Mock<IPdfService> _pdfServiceMock;
        private readonly BookController _controller;
        private readonly ITempDataDictionary _tempData;
        private readonly Mock<IServiceProvider> _serviceProviderMock;

        public BookControllerTests()
        {
            _unitOfWorkMock = new Mock<IUnitOfWork>();
            _bookingServiceMock = new Mock<IBookingService>();
            _loggerMock = new Mock<ILogger<BookController>>();
            _emailSenderMock = new Mock<IEmailSender>();
            _flightSeatRepoMock = new Mock<IGenericRepository<FlightSeat>>();
            _addOnRepoMock = new Mock<IGenericRepository<AddOn>>();
            _pdfServiceMock = new Mock<IPdfService>();


            _unitOfWorkMock.Setup(u => u.Repository<FlightSeat>()).Returns(_flightSeatRepoMock.Object);
            _unitOfWorkMock.Setup(u => u.Repository<AddOn>()).Returns(_addOnRepoMock.Object);

            _controller = new BookController(
                _unitOfWorkMock.Object,
                _bookingServiceMock.Object,
                _loggerMock.Object,
                _pdfServiceMock.Object,
                _emailSenderMock.Object
            );

            // Setup HttpContext with user claims
            var user = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
            {
                new Claim(ClaimTypes.NameIdentifier, "test-user-id")
            }, "mock"));

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            };

            // Setup TempData
            _tempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>());
            _controller.TempData = _tempData;

            // Setup UrlHelper for success/cancel URLs
            var urlHelperMock = new Mock<IUrlHelper>();
            urlHelperMock.Setup(u => u.Action(It.IsAny<UrlActionContext>()))
                .Returns("https://localhost/success");
            _controller.Url = urlHelperMock.Object;

            // Setup Request.Scheme
            _controller.Request.Scheme = "https";

            // Setup ServiceProvider for IEmailSender in Success action
            _serviceProviderMock = new Mock<IServiceProvider>();
            _serviceProviderMock.Setup(sp => sp.GetService(typeof(IEmailSender))).Returns(_emailSenderMock.Object);
            _controller.ControllerContext.HttpContext.RequestServices = _serviceProviderMock.Object;
        }

        #region Book GET

        [Fact]
        public async Task Book_Get_InvalidTicketCount_ShouldRedirectToFlightIndexWithError()
        {
            // Arrange
            int flightId = 1;
            int ticketCount = 0;

            // Act
            var result = await _controller.Book(flightId, ticketCount);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Index", redirectResult.ActionName);
            Assert.Equal("Flight", redirectResult.ControllerName);
            Assert.Equal(SD.Customer, redirectResult.RouteValues["area"]);
            Assert.Equal("Invalid ticket count. Please select at least one ticket.", _controller.TempData["error"]);
        }

        [Fact]
        public async Task Book_Get_BookingServiceReturnsFailure_ShouldReturnSeatsNotAvailableViewWithError()
        {
            // Arrange
            int flightId = 1;
            int ticketCount = 2;
            var serviceResult = Result<BookingCreateViewModel>.Failure(new Error("Seat_001", "Seats not available"));
            _bookingServiceMock.Setup(s => s.GetBookingAsync(flightId, ticketCount))
                .ReturnsAsync(serviceResult);

            // Act
            var result = await _controller.Book(flightId, ticketCount);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal("SeatsNotAvailable", viewResult.ViewName);
            Assert.Equal("Seats not available", _controller.ViewBag.ErrorMessage);
            Assert.Equal(flightId, _controller.ViewBag.FlightId);
        }

        [Fact]
        public async Task Book_Get_Success_ShouldReturnViewWithBookingCreateViewModel()
        {
            // Arrange
            int flightId = 1;
            int ticketCount = 2;
            var expectedViewModel = new BookingCreateViewModel { FlightId = flightId };
            var serviceResult = Result<BookingCreateViewModel>.Success(expectedViewModel);

            _bookingServiceMock.Setup(s => s.GetBookingAsync(flightId, ticketCount))
                .ReturnsAsync(serviceResult);

            // Act
            var result = await _controller.Book(flightId, ticketCount);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<BookingCreateViewModel>(viewResult.Model);
            Assert.Equal(flightId, model.FlightId);
        }

        #endregion

        #region Book POST

        [Fact]
        public async Task Book_Post_UserNotAuthenticated_ShouldReturnUnauthorized()
        {
            // Arrange
            _controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(); // empty user
            var model = new BookingCreateViewModel();

            // Act
            var result = await _controller.Book(model);

            // Assert
            var unauthorizedResult = Assert.IsType<UnauthorizedResult>(result);
            Assert.Equal("You must be logged in to make a booking.", _controller.ViewBag.ErrorMessage);
        }

        [Fact]
        public async Task Book_Post_InvalidModelState_ShouldReturnViewAndPopulateSeatsAddOns()
        {
            // Arrange
            var model = new BookingCreateViewModel { FlightId = 1 };
            _controller.ModelState.AddModelError("Tickets", "Required");

            var flightSeats = new List<FlightSeat>
            {
                new FlightSeat { Id = 1, Seat = new Seat { Row = "A", Number = 1, Class = SeatClass.Economy, Price = 100 } }
            };
            var addOns = new List<AddOn> { new AddOn { Id = 1, Name = "Baggage" } };

            _flightSeatRepoMock.Setup(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<FlightSeat>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(flightSeats);
            _addOnRepoMock.Setup(r => r.GetAllAsync())
                .ReturnsAsync(addOns);

            // Act
            var result = await _controller.Book(model);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal(model, viewResult.Model);
            Assert.NotNull(model.AvailableSeats);
            Assert.Single(model.AvailableSeats);
            Assert.NotNull(model.AddOns);
            Assert.Single(model.AddOns);
        }

        [Fact]
        public async Task Book_Post_CreateBookingFails_ShouldReturnViewWithError()
        {
            // Arrange
            var model = new BookingCreateViewModel { FlightId = 1, Tickets = new List<TicketBookingVM> { new TicketBookingVM() } };
            var userId = "test-user-id";

            _bookingServiceMock.Setup(s => s.CreateBookingAsync(model, userId))
                .ReturnsAsync(Result<int>.Failure(new Error("Book_001", "Booking failed")));


            // Act
            var result = await _controller.Book(model);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal("Booking failed", _controller.ViewBag.ErrorMessage);
            _bookingServiceMock.Verify(s => s.CreatePaymentAsync(It.IsAny<int>(), It.IsAny<decimal>()), Times.Never);
        }

        [Fact]
        public async Task Book_Post_CreatePaymentFails_ShouldReturnViewWithError()
        {
            // Arrange
            var model = new BookingCreateViewModel { FlightId = 1, Tickets = new List<TicketBookingVM> { new TicketBookingVM { TicketPrice = 100 } } };
            var userId = "test-user-id";

            _bookingServiceMock.Setup(s => s.CreateBookingAsync(model, userId))
                .ReturnsAsync(Result<int>.Success(123));
            _bookingServiceMock.Setup(s => s.CreatePaymentAsync(123, 100))
                .ReturnsAsync(Result<Payment>.Failure(new Error("Book_001", "Payment failed")));

            // Act
            var result = await _controller.Book(model);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal("Payment failed", _controller.ViewBag.ErrorMessage);
            _bookingServiceMock.Verify(s => s.CreateStripeSessionAsync(It.IsAny<Payment>(), It.IsAny<BookingCreateViewModel>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task Book_Post_CreateStripeSessionFails_ShouldReturnViewWithError()
        {
            // Arrange
            var model = new BookingCreateViewModel { FlightId = 1, Tickets = new List<TicketBookingVM> { new TicketBookingVM { TicketPrice = 100 } } };
            var userId = "test-user-id";
            var payment = new Payment { Id = 1 };

            _bookingServiceMock.Setup(s => s.CreateBookingAsync(model, userId))
                .ReturnsAsync(Result<int>.Success(123));
            _bookingServiceMock.Setup(s => s.CreatePaymentAsync(123, 100))
                .ReturnsAsync(Result<Payment>.Success(payment));
            _bookingServiceMock.Setup(s => s.CreateStripeSessionAsync(payment, model, It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(Result<string>.Failure(new Error("Book_001", "Stripe failed")));

            // Act
            var result = await _controller.Book(model);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal("Stripe failed", _controller.ViewBag.ErrorMessage);
        }

        [Fact]
        public async Task Book_Post_Success_ShouldRedirectToStripeSessionUrl()
        {
            // Arrange
            var model = new BookingCreateViewModel { FlightId = 1, Tickets = new List<TicketBookingVM> { new TicketBookingVM { TicketPrice = 100 } } };
            var userId = "test-user-id";
            var payment = new Payment { Id = 1 };
            var stripeUrl = "https://checkout.stripe.com/session";

            _bookingServiceMock.Setup(s => s.CreateBookingAsync(model, userId))
                .ReturnsAsync(Result<int>.Success(123));
            _bookingServiceMock.Setup(s => s.CreatePaymentAsync(123, 100))
                .ReturnsAsync(Result<Payment>.Success(payment));
            _bookingServiceMock.Setup(s => s.CreateStripeSessionAsync(payment, model, It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(Result<string>.Success(stripeUrl));

            // Act
            var result = await _controller.Book(model);

            // Assert
            var redirectResult = Assert.IsType<RedirectResult>(result);
            Assert.Equal(stripeUrl, redirectResult.Url);
        }

        [Fact]
        public async Task Book_Post_Exception_ShouldReturnViewWithError()
        {
            // Arrange
            var model = new BookingCreateViewModel { FlightId = 1 };
            var userId = "test-user-id";

            _bookingServiceMock.Setup(s => s.CreateBookingAsync(model, userId))
                .ThrowsAsync(new Exception("Unexpected error"));

            // Act
            var result = await _controller.Book(model);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal("An unexpected error occurred during booking. Please try again.", _controller.ViewBag.ErrorMessage);
        }

        #endregion

        #region Cancel

        [Fact]
        public async Task Cancel_ShouldReturnView_WhenCancellationSucceeds()
        {
            // Arrange
            int paymentId = 1;
            _bookingServiceMock.Setup(s => s.CancelBookingAsync(paymentId))
                .ReturnsAsync(Result<bool>.Success(true));

            // Act
            var result = await _controller.Cancel(paymentId);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal("Booking cancelled successfully. No charges have been made to your account.", _controller.TempData["success"]);
        }

        [Fact]
        public async Task Cancel_ShouldReturnViewWithError_WhenCancellationFails()
        {
            // Arrange
            int paymentId = 1;
            _bookingServiceMock.Setup(s => s.CancelBookingAsync(paymentId))
                .ReturnsAsync(Result<bool>.Failure(new Error("Cancel_001", "Cancel failed")));

            // Act
            var result = await _controller.Cancel(paymentId);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal("Cancel failed", _controller.ViewBag.ErrorMessage);
        }

        [Fact]
        public async Task Cancel_ShouldReturnViewWithError_WhenExceptionOccurs()
        {
            // Arrange
            int paymentId = 1;
            _bookingServiceMock.Setup(s => s.CancelBookingAsync(paymentId))
                .ThrowsAsync(new Exception("Error"));

            // Act
            var result = await _controller.Cancel(paymentId);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal("An unexpected error occurred while cancelling the booking.", _controller.ViewBag.ErrorMessage);
        }

        #endregion

        #region Success

        [Fact]
        public async Task Success_UserNotAuthenticated_ShouldReturnUnauthorized()
        {
            // Arrange
            _controller.ControllerContext.HttpContext.User = new ClaimsPrincipal();
            int paymentId = 1;

            // Act
            var result = await _controller.Success(paymentId);

            // Assert
            var unauthorizedResult = Assert.IsType<UnauthorizedResult>(result);
            Assert.Equal("You must be logged in to view this page.", _controller.ViewBag.ErrorMessage);
        }

        [Fact]
        public async Task Success_ConfirmPaymentFails_ShouldRedirectToMyBookingsWithError()
        {
            // Arrange
            int paymentId = 1;
            var userId = "test-user-id";

            _bookingServiceMock.Setup(s => s.ConfirmPaymentAsync(paymentId, userId))
                .ReturnsAsync(Result<Booking>.Failure(new Error("Success_001", "Confirmation failed")));
            // Act
            var result = await _controller.Success(paymentId);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Index", redirectResult.ActionName);
            Assert.Equal("MyBookings", redirectResult.ControllerName);
            Assert.Equal(SD.Customer, redirectResult.RouteValues["area"]);
            Assert.Equal("Confirmation failed", _controller.TempData["error"]);
        }

        [Fact]
        public async Task Success_ConfirmPaymentSuccess_ShouldSendEmailAndReturnView()
        {
            // Arrange
            int paymentId = 1;
            var userId = "test-user-id";
            var booking = new Booking
            {
                Id = 123,
                PNR = "ABC123",
                FlightId = 1,
                TotalPrice = 787,
                Status = Status.Pending,
                AppUser = new AppUser { Email = "customer@test.com", FirstName = "Ali" }
            };

            _bookingServiceMock.Setup(s => s.ConfirmPaymentAsync(paymentId, userId))
                .ReturnsAsync(Result<Booking>.Success(booking));

            _pdfServiceMock.Setup(p => p.GenerateBookingPdfAsync(
                    booking,
                    It.IsAny<ViewDataDictionary>(),
                    It.IsAny<ActionContext>()))
                .ReturnsAsync(new byte[] { 1, 2, 3 });

            _emailSenderMock.Setup(e => e.SendEmailAsyncWithAttachment(
                    booking.AppUser.Email,
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<byte[]>(),
                    It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            // Create a fresh controller instance with all dependencies
            var controller = new BookController(
                _unitOfWorkMock.Object,
                _bookingServiceMock.Object,
                _loggerMock.Object,
                _pdfServiceMock.Object,
                _emailSenderMock.Object);

            // Set up HttpContext with user claims
            var httpContext = new DefaultHttpContext();
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId) }, "TestAuth"));
            httpContext.Request.Scheme = "https";
            httpContext.RequestServices = Mock.Of<IServiceProvider>(); 

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext,
                RouteData = new Microsoft.AspNetCore.Routing.RouteData(),
                ActionDescriptor = new Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor()
            };

            // Set ViewData to a non‑null instance
            controller.ViewData = new ViewDataDictionary(
                new Microsoft.AspNetCore.Mvc.ModelBinding.EmptyModelMetadataProvider(),
                new Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary());

            // Set TempData
            controller.TempData = _tempData;

            var urlHelperMock = new Mock<IUrlHelper>();
            urlHelperMock.Setup(u => u.Action(It.IsAny<UrlActionContext>())).Returns("https://localhost/success");
            controller.Url = urlHelperMock.Object;

            // Act
            var result = await controller.Success(paymentId);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<int>(viewResult.Model);
            Assert.Equal(booking.Id, model);
            Assert.Equal($"Booking confirmed successfully! Your PNR is: {booking.PNR}. A confirmation email has been sent.", controller.TempData["success"]);

            // Verify the PDF service was called exactly once
            _pdfServiceMock.Verify(p => p.GenerateBookingPdfAsync(
                booking,
                It.IsAny<ViewDataDictionary>(),
                It.IsAny<ActionContext>()), Times.Once);

            // Verify the email was sent with the correct attachment name
            _emailSenderMock.Verify(e => e.SendEmailAsyncWithAttachment(
                booking.AppUser.Email,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<byte[]>(),
                $"Booking_{booking.PNR}.pdf"), Times.Once);
        }

        [Fact]
        public async Task Success_Exception_ShouldRedirectToMyBookingsWithError()
        {
            // Arrange
            int paymentId = 1;
            var userId = "test-user-id";

            _bookingServiceMock.Setup(s => s.ConfirmPaymentAsync(paymentId, userId))
                .ThrowsAsync(new Exception("Unexpected"));

            // Act
            var result = await _controller.Success(paymentId);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Index", redirectResult.ActionName);
            Assert.Equal("MyBookings", redirectResult.ControllerName);
            Assert.Equal(SD.Customer, redirectResult.RouteValues["area"]);
            Assert.Equal("An error occurred while confirming your payment. Please contact support.", _controller.TempData["error"]);
        }

        #endregion
    }
}