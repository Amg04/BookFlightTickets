using BookFlightTickets.Core.Domain.Entities;
using BookFlightTickets.Core.Domain.RepositoryContracts;
using BookFlightTickets.Core.Domain.ResultPattern;
using BookFlightTickets.Core.ServiceContracts;
using BookFlightTickets.Core.ViewModels;
using BookFlightTickets.UI;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace BookFlightTickets.IntegrationTests.Customer
{
    public class BookIntegrationTests : BaseIntegrationTest
    {
        private readonly Mock<IBookingService> _bookingServiceMock;
        private readonly Mock<IPdfService> _pdfServiceMock;
        private readonly Mock<IEmailSender> _emailSenderMock;

        public BookIntegrationTests(WebApplicationFactory<Program> factory) : base(factory)
        { 
            _bookingServiceMock = new Mock<IBookingService>();
            _pdfServiceMock = new Mock<IPdfService>();
            _emailSenderMock = new Mock<IEmailSender>();

            InitializeFactory(services =>
            {
                services.AddScoped(_ => _bookingServiceMock.Object);
                services.AddScoped(_ => _pdfServiceMock.Object);
                services.AddScoped(_ => _emailSenderMock.Object);
            });
        }

        #region Book - GET


        [Fact]
        public async Task Book_Get_ValidRequest_ReturnsViewWithModel()
        {
            // Arrange
            var flightId = 1;
            var ticketCount = 2;
            var expectedModel = new BookingCreateViewModel
            {
                FlightId = flightId,
                Tickets = new List<TicketBookingVM>(),
                AvailableSeats = new List<SelectListItem>(),
                AddOns = new List<AddOn>()
            };

            _bookingServiceMock.Setup(s => s.GetBookingAsync(flightId, ticketCount))
                .ReturnsAsync(Result<BookingCreateViewModel>.Success(expectedModel));

            var client = CreateAuthenticatedClient();

            // Act
            var response = await client.GetAsync($"/Customer/Book/Book?flightId={flightId}&ticketCount={ticketCount}");
            var result = await response.EnsureSuccessStatusCode().Content.ReadAsStringAsync();

            // Assert
            Assert.Contains("Book", result); 
            _bookingServiceMock.Verify(s => s.GetBookingAsync(flightId, ticketCount), Times.Once);
        }

        [Fact]
        public async Task Book_Get_InvalidTicketCount_RedirectsToFlightIndex()
        {
            var client = CreateAuthenticatedClient();
            // Act
            var response = await client.GetAsync("/Customer/Book/Book?flightId=1&ticketCount=0");

            // Assert
            Assert.Equal(StatusCodes.Status302Found, (int)response.StatusCode);
            Assert.Equal("/Customer/Flight", response.Headers.Location?.OriginalString);
        }

        [Fact]
        public async Task Book_Get_ServiceReturnsFailure_ReturnsSeatsNotAvailableView()
        {
            // Arrange
            var flightId = 1;
            var ticketCount = 2;
            _bookingServiceMock.Setup(s => s.GetBookingAsync(flightId, ticketCount))
                .ReturnsAsync(Result<BookingCreateViewModel>.Failure(new Error("SEAT_001", "No seats available")));

            var client = CreateAuthenticatedClient();
            // Act
            var response = await client.GetAsync($"/Customer/Book/Book?flightId={flightId}&ticketCount={ticketCount}");
            var content = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Contains("Seats Not Available", content);
        }

        [Fact]
        public async Task Book_Get_UnauthorizedUser_RedirectsToLogin()
        {
            var client = CreateUnauthenticatedClient();

            // Act
            var response = await client.GetAsync("/Customer/Book/Book?flightId=1&ticketCount=2");

            // Assert
            Assert.Equal(StatusCodes.Status302Found, (int)response.StatusCode);
            var location = response.Headers.Location;
            Assert.NotNull(location);
            Assert.Equal("/Identity/Account/Login", location.LocalPath);
        }

        #endregion

        #region Book - POST

        [Fact]
        public async Task Book_Post_ValidModel_CreatesBookingAndRedirectsToStripe()
        {
            // Arrange
            var model = new BookingCreateViewModel
            {
                FlightId = 1,
                Tickets = new List<TicketBookingVM>
                {
                    new TicketBookingVM
                    {
                        SelectedSeatId = 10,
                        TicketPrice = 100,
                        SelectedAddOnIds = new List<int> { 1, 2 },
                        FirstName = "John",
                        LastName = "Doe",
                        PassportNumber = "AB123"
                    }
                }
            };
            var userId = "test-user-id";
            var bookingId = 42;
            var payment = new Payment { Id = 5, Amount = 100 };
            const string stripeSessionUrl = "https://checkout.stripe.com/session";

            _bookingServiceMock.Setup(s => s.CreateBookingAsync(It.IsAny<BookingCreateViewModel>(), userId))
                .ReturnsAsync(Result<int>.Success(bookingId));
            _bookingServiceMock.Setup(s => s.CreatePaymentAsync(bookingId, 100))
                .ReturnsAsync(Result<Payment>.Success(payment));
            _bookingServiceMock.Setup(s => s.CreateStripeSessionAsync(payment, It.IsAny<BookingCreateViewModel>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(Result<string>.Success(stripeSessionUrl));
           
            var formData = new MultipartFormDataContent
            {
                { new StringContent(model.FlightId.ToString()), "FlightId" },
                { new StringContent(model.Tickets[0].SelectedSeatId.ToString()), "Tickets[0].FlightSeatId" },
                { new StringContent(model.Tickets[0].TicketPrice.ToString()), "Tickets[0].TicketPrice" },
                { new StringContent("1"), "Tickets[0].AddOnIds[0]" },
                { new StringContent("2"), "Tickets[0].AddOnIds[1]" }
            };

            var client = CreateAuthenticatedClient();
            // Act
            var response = await client.PostAsync("/Customer/Book/Book", formData);

            // Assert
            Assert.Equal(StatusCodes.Status302Found, (int)response.StatusCode);
            Assert.Equal(stripeSessionUrl, response.Headers.Location?.OriginalString);
            _bookingServiceMock.Verify(s => s.CreateBookingAsync(It.IsAny<BookingCreateViewModel>(), userId), Times.Once);
        }

        [Fact]
        public async Task Book_Post_ModelInvalid_ReturnsViewWithErrors()
        {
            // Arrange
            var formData = new MultipartFormDataContent
            {
                { new StringContent("1"), "FlightId" },                    
                { new StringContent(""), "Tickets[0].FirstName" },         
                { new StringContent(""), "Tickets[0].LastName" },          
                { new StringContent(""), "Tickets[0].PassportNumber" }     
            };

            var client = CreateAuthenticatedClient();

            // Act
            var response = await client.PostAsync("/Customer/Book/Book", formData);
            var content = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(StatusCodes.Status200OK, (int)response.StatusCode);
            Assert.Matches("First.*?Name.*?required", content);
        }

        [Fact]
        public async Task Book_Post_CreateBookingFails_ReturnsViewWithError()
        {
            // Arrange
            var model = new BookingCreateViewModel { FlightId = 1, Tickets = new List<TicketBookingVM>() };
            var userId = "test-user-id";
            _bookingServiceMock.Setup(s => s.CreateBookingAsync(It.IsAny<BookingCreateViewModel>(), userId))
                .ReturnsAsync(Result<int>.Failure(new Error("Booking_001", "Booking failed")));
          
            var formData = new MultipartFormDataContent
            {
                { new StringContent(model.FlightId.ToString()), "FlightId" }
            };

            var client = CreateAuthenticatedClient();

            // Act
            var response = await client.PostAsync("/Customer/Book/Book", formData);
            var content = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Contains("toastr.error('Booking failed');", content);
        }

        [Fact]
        public async Task Book_Post_CreatePaymentFails_ReturnsViewWithError()
        {
            // Arrange
            var model = new BookingCreateViewModel
            {
                FlightId = 1,
                Tickets = new List<TicketBookingVM> { new TicketBookingVM { TicketPrice = 100 } }
            };
            var userId = "test-user-id";
            var bookingId = 42;

            _bookingServiceMock.Setup(s => s.CreateBookingAsync(It.IsAny<BookingCreateViewModel>(), userId))
                .ReturnsAsync(Result<int>.Success(bookingId));
            _bookingServiceMock.Setup(s => s.CreatePaymentAsync(bookingId, 100))
                .ReturnsAsync(Result<Payment>.Failure(new Error("Payment_001", "Failed to create payment")));

            var formData = new MultipartFormDataContent
            {
                { new StringContent(model.FlightId.ToString()), "FlightId" },
                { new StringContent("100"), "Tickets[0].TicketPrice" }
            };

            var client = CreateAuthenticatedClient();

            // Act
            var response = await client.PostAsync("/Customer/Book/Book", formData);
            var content = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Contains("toastr.error('Failed to create payment');", content);
        }

        [Fact]
        public async Task Book_Post_StripeSessionFails_ReturnsViewWithError()
        {
            // Arrange
            var model = new BookingCreateViewModel
            {
                FlightId = 1,
                Tickets = new List<TicketBookingVM> {
            new TicketBookingVM {
                TicketPrice = 100,
                SelectedSeatId = 1,
                FirstName = "John",
                LastName = "Doe",
                PassportNumber = "AB123"
                }
                }
            };

            var userId = "test-user-id";
            var bookingId = 42;
            var payment = new Payment { Id = 5 };

            _bookingServiceMock.Setup(s => s.CreateBookingAsync(It.IsAny<BookingCreateViewModel>(), userId))
                .ReturnsAsync(Result<int>.Success(bookingId));
            _bookingServiceMock.Setup(s => s.CreatePaymentAsync(bookingId, 100))
                .ReturnsAsync(Result<Payment>.Success(payment));
            _bookingServiceMock.Setup(s => s.CreateStripeSessionAsync(payment, It.IsAny<BookingCreateViewModel>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(Result<string>.Failure(new Error("Stripe_001", "Stripe error")));

            var formData = new MultipartFormDataContent
            {
                { new StringContent(model.FlightId.ToString()), "FlightId" },
                { new StringContent(model.Tickets[0].TicketPrice.ToString()), "Tickets[0].TicketPrice" },
                { new StringContent(model.Tickets[0].SelectedSeatId.ToString()), "Tickets[0].SelectedSeatId" },
                { new StringContent(model.Tickets[0].FirstName), "Tickets[0].FirstName" },
                { new StringContent(model.Tickets[0].LastName), "Tickets[0].LastName" },
                { new StringContent(model.Tickets[0].PassportNumber), "Tickets[0].PassportNumber" }
            };

            // Act
            var client = CreateAuthenticatedClient();

            // Act
            var response = await client.PostAsync("/Customer/Book/Book", formData);
            var content = await response.Content.ReadAsStringAsync();

            // Assert
            _bookingServiceMock.Verify(s => s.CreateStripeSessionAsync(payment, It.IsAny<BookingCreateViewModel>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            Assert.Contains("toastr.error('Stripe error');", content);
        }

        [Fact]
        public async Task Book_Post_ExceptionThrown_ReturnsViewWithError()
        {
            // Arrange
            var model = new BookingCreateViewModel { FlightId = 1 };
            var userId = "test-user-id";
            _bookingServiceMock.Setup(s => s.CreateBookingAsync(model, userId))
                .ThrowsAsync(new Exception("Database error"));

            var formData = new MultipartFormDataContent
            {
                { new StringContent(model.FlightId.ToString()), "FlightId" }
            };

            var client = CreateAuthenticatedClient();

            // Act
            var response = await client.PostAsync("/Customer/Book/Book", formData);
            var content = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Contains("An unexpected error occurred", content);
        }

        [Fact]
        public async Task Book_Post_UnauthorizedUser_RedirectsToLogin()
        {
            var client = CreateUnauthenticatedClient();
            var formData = new MultipartFormDataContent
            {
                { new StringContent("1"), "FlightId" }
            };

            // Act
            var response = await client.PostAsync("/Customer/Book/Book", formData);

            // Assert
            Assert.Equal(StatusCodes.Status302Found, (int)response.StatusCode);
            var location = response.Headers.Location;
            Assert.NotNull(location);
            Assert.Equal("/Identity/Account/Login", location.LocalPath);
        }

        #endregion

        #region Success


        [Fact]
        public async Task Success_ValidPayment_ConfirmsBookingAndSendsEmail()
        {
            // Arrange
            var paymentId = 5;
            var userId = "test-user-id";
            var booking = new Booking
            {
                Id = 10,
                PNR = "ABC123",
                AppUser = new AppUser { Email = "customer@test.com" }
            };
            var pdfBytes = new byte[] { 1, 2, 3 };

            _bookingServiceMock.Setup(s => s.ConfirmPaymentAsync(paymentId, userId))
                .ReturnsAsync(Result<Booking>.Success(booking));
            _pdfServiceMock.Setup(s => s.GenerateBookingPdfAsync(booking, It.IsAny<ViewDataDictionary>(), It.IsAny<ActionContext>()))
                .ReturnsAsync(pdfBytes);
            _emailSenderMock.Setup(s => s.SendEmailAsyncWithAttachment(
                booking.AppUser.Email,
                It.IsAny<string>(),
                It.IsAny<string>(),
                pdfBytes,
                It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            var client = CreateAuthenticatedClient();

            // Act
            var response = await client.GetAsync($"/Customer/Book/Success/{paymentId}");
            var content = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(StatusCodes.Status200OK, (int)response.StatusCode);
            Assert.Contains("ABC123", content);
            _emailSenderMock.Verify(s => s.SendEmailAsyncWithAttachment(
                booking.AppUser.Email,
                It.IsAny<string>(),
                It.IsAny<string>(),
                pdfBytes,
                It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task Success_ConfirmationFails_RedirectsToMyBookingsWithError()
        {
            // Arrange
            var paymentId = 5;
            var userId = "test-user-id";
            _bookingServiceMock.Setup(s => s.ConfirmPaymentAsync(paymentId, userId))
                .ReturnsAsync(Result<Booking>.Failure(new Error("Booking_002", "Confirmation failed")));

            var client = CreateAuthenticatedClient();

            // Act
            var response = await client.GetAsync($"/Customer/Book/Success/{paymentId}");

            // Assert
            _bookingServiceMock.Verify(s => s.ConfirmPaymentAsync(paymentId, userId), Times.Once);
            Assert.Equal(StatusCodes.Status302Found, (int)response.StatusCode);
            Assert.Equal("/Customer/MyBookings", response.Headers.Location?.OriginalString);
        }

        [Fact]
        public async Task Success_ExceptionThrown_RedirectsToMyBookingsWithError()
        {
            // Arrange
            var paymentId = 5;
            var userId = "test-user-id";
            _bookingServiceMock.Setup(s => s.ConfirmPaymentAsync(paymentId, userId))
                .ThrowsAsync(new Exception("Unexpected error"));

            var client = CreateAuthenticatedClient();

            // Act
            var response = await client.GetAsync($"/Customer/Book/Success/{paymentId}");

            // Assert
            Assert.Equal(StatusCodes.Status302Found, (int)response.StatusCode);
            Assert.Equal("/Customer/MyBookings", response.Headers.Location?.OriginalString);
        }

        [Fact]
        public async Task Success_UnauthorizedUser_RedirectsToLogin()
        {
            var client = CreateUnauthenticatedClient();

            // Act
            var response = await client.GetAsync("/Customer/Book/Success/5");

            // Assert
            // Assert
            Assert.Equal(StatusCodes.Status302Found, (int)response.StatusCode);
            var location = response.Headers.Location;
            Assert.NotNull(location);
            Assert.Equal("/Identity/Account/Login", location.LocalPath);
        }

        #endregion

        #region Cancel


        [Fact]
        public async Task Cancel_ValidRequest_ReturnsViewWithSuccessMessage()
        {
            // Arrange
            var paymentId = 5;
            _bookingServiceMock.Setup(s => s.CancelBookingAsync(paymentId))
                .ReturnsAsync(Result<bool>.Success(true));

            var client = CreateAuthenticatedClient();

            // Act
            var response = await client.GetAsync($"/Customer/Book/Cancel/{paymentId}");
            var content = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Contains("Booking cancelled successfully", content);
            Assert.Contains("toastr.success('Booking cancelled successfully. No charges have been made to your account.');", content);
        }

        [Fact]
        public async Task Cancel_CancellationFails_ReturnsViewWithErrorMessage()
        {
            // Arrange
            var paymentId = 5;
            _bookingServiceMock.Setup(s => s.CancelBookingAsync(paymentId))
                .ReturnsAsync(Result<bool>.Failure(new Error("Booking_001","Cancellation failed")));


            var client = CreateAuthenticatedClient();

            // Act
            var response = await client.GetAsync($"/Customer/Book/Cancel/{paymentId}");
            var content = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Contains("toastr.error('Cancellation failed');", content);
        }

        [Fact]
        public async Task Cancel_ExceptionThrown_ReturnsViewWithErrorMessage()
        {
            // Arrange
            var paymentId = 5;
            _bookingServiceMock.Setup(s => s.CancelBookingAsync(paymentId))
                .ThrowsAsync(new Exception("Server error"));

            var client = CreateAuthenticatedClient();

            // Act
            var response = await client.GetAsync($"/Customer/Book/Cancel/{paymentId}");
            var content = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Contains("An unexpected error occurred", content);
        }

        [Fact]
        public async Task Cancel_UnauthorizedUser_RedirectsToLogin()
        {
            var client = CreateUnauthenticatedClient();

            // Act
            var response = await client.GetAsync("/Customer/Book/Cancel/5");

            // Assert
            Assert.Equal(StatusCodes.Status302Found, (int)response.StatusCode);
            var location = response.Headers.Location;
            Assert.NotNull(location);
            Assert.Equal("/Identity/Account/Login", location.LocalPath);
        }

        #endregion

    }
}

 