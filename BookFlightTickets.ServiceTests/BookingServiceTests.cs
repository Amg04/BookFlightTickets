using AutoFixture;
using AutoFixture.AutoMoq;
using BookFlightTickets.Core.Domain.Entities;
using BookFlightTickets.Core.Domain.Hubs;
using BookFlightTickets.Core.Domain.RepositoryContracts;
using BookFlightTickets.Core.Domain.Specifications;
using BookFlightTickets.Core.ServiceContracts;
using BookFlightTickets.Core.Services;
using BookFlightTickets.Core.ViewModels;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Stripe;
using Stripe.Checkout;
namespace BookFlightTickets.ServiceTests
{
    public class BookingServiceTests
    {

        private readonly IFixture _fixture;
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly Mock<IRedisCacheService> _cacheServiceMock;
        private readonly Mock<IStripeSessionService> _stripeSessionMock;
        private readonly ILogger<BookingService> _logger;
        private readonly Mock<IHubContext<DashboardHub>> _hubContextMock;
        private readonly Mock<IClientProxy> _clientProxyMock;
        private readonly BookingService _sut;

        private readonly Dictionary<Type, object> _repositoryMocks = new();

        public BookingServiceTests()
        {
            _fixture = new Fixture().Customize(new AutoMoqCustomization());
            _fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList()
                .ForEach(b => _fixture.Behaviors.Remove(b));
            _fixture.Behaviors.Add(new OmitOnRecursionBehavior());

            _unitOfWorkMock = new Mock<IUnitOfWork>();
            _cacheServiceMock = new Mock<IRedisCacheService>();
            _stripeSessionMock = new Mock<IStripeSessionService>();
            _logger = NullLogger<BookingService>.Instance;
            _hubContextMock = new Mock<IHubContext<DashboardHub>>();
            _clientProxyMock = new Mock<IClientProxy>();  

            var mockClients = new Mock<IHubClients>();
            mockClients.Setup(clients => clients.Group(It.IsAny<string>())).Returns(_clientProxyMock.Object);
            _hubContextMock.Setup(h => h.Clients).Returns(mockClients.Object);

            SetupRepositoryMock<Flight>();
            SetupRepositoryMock<FlightSeat>();
            SetupRepositoryMock<AddOn>();
            SetupRepositoryMock<Booking>();
            SetupRepositoryMock<Ticket>();
            SetupRepositoryMock<TicketAddOns>();
            SetupRepositoryMock<Payment>();

            _sut = new BookingService(
            _unitOfWorkMock.Object,
            _logger,
            _cacheServiceMock.Object,
            _stripeSessionMock.Object,
            _hubContextMock.Object);
        }

        private Mock<IGenericRepository<TEntity>> GetRepositoryMock<TEntity>() where TEntity : BaseClass
        {
            if (_repositoryMocks.TryGetValue(typeof(TEntity), out var mock))
            {
                return (Mock<IGenericRepository<TEntity>>)mock;
            }

            var newMock = new Mock<IGenericRepository<TEntity>>();
            _repositoryMocks[typeof(TEntity)] = newMock;
            _unitOfWorkMock.Setup(u => u.Repository<TEntity>()).Returns(newMock.Object);
            return newMock;
        }

        private void SetupRepositoryMock<TEntity>() where TEntity : BaseClass => GetRepositoryMock<TEntity>();

        #region GetBookingAsync

        public class GetBookingAsyncTests : BookingServiceTests
        {
            [Fact]
            public async Task Should_ReturnSuccess_WithViewModel_WhenFlightExistsAndSeatsAvailable()
            {
                // Arrange
                var flightId = _fixture.Create<int>();
                var ticketCount = 2;
                var flight = _fixture.Build<Flight>()
                    .With(f => f.Id, flightId)
                    .Create();
                var availableSeatsCount = 5;

                var flightSeats = _fixture.Build<FlightSeat>()
                    .With(s => s.FlightId, flightId)
                    .With(s => s.IsAvailable, true)
                    .With(s => s.Seat, _fixture.Create<Seat>())
                    .CreateMany(3).ToList();

                var addOns = _fixture.CreateMany<AddOn>(4).ToList();

                _cacheServiceMock
                    .Setup(c => c.GetOrSetAsync(
                        It.Is<string>(k => k == $"flight:{flightId}"),
                        It.IsAny<Func<Task<Flight>>>(),
                        It.IsAny<TimeSpan>()))
                    .ReturnsAsync(flight);

                // we said if he call CountAsync return availableSeatsCount (5)
                var flightSeatRepoMock = GetRepositoryMock<FlightSeat>();
                flightSeatRepoMock.Setup(r => r.CountAsync(It.IsAny<ISpecification<FlightSeat>>()))
                    .ReturnsAsync(availableSeatsCount);

                // we said if he call GetAllWithSpecAsync return by flightSeats (the ones i made)
                flightSeatRepoMock
                    .Setup(r => r.GetAllWithSpecAsync(
                        It.IsAny<ISpecification<FlightSeat>>(),
                        It.IsAny<CancellationToken>()))
                    .ReturnsAsync(flightSeats);

                // we are pretending there 's no addon in the cache
                var addOnRepoMock = GetRepositoryMock<AddOn>();
                _cacheServiceMock.Setup(c => c.GetAsync<List<AddOn>>(It.Is<string>(k => k == "addons:all")))
                    .ReturnsAsync((List<AddOn>?)null);

                addOnRepoMock
                    .Setup(r => r.GetAllAsync())
                    .ReturnsAsync(addOns);
                _cacheServiceMock
                    .Setup(c => c.SetAsync("addons:all", addOns, It.IsAny<TimeSpan>()))
                    .Returns(Task.CompletedTask);

                // Act
                var result = await _sut.GetBookingAsync(flightId, ticketCount);

                // Assert
                result.IsSuccess.Should().BeTrue();
                result.Value.Should().NotBeNull();
                result.Value.FlightId.Should().Be(flightId);
                result.Value.Flight.Should().Be(flight);
                result.Value.AvailableSeats.Should().HaveSameCount(flightSeats);
                result.Value.AddOns.Should().BeEquivalentTo(addOns);
                result.Value.Tickets.Should().HaveCount(ticketCount);
            }

            [Fact]
            public async Task Should_ReturnFailure_WhenFlightNotFound()
            {
                // Arrange
                var flightId = _fixture.Create<int>();
                _cacheServiceMock
                    .Setup(c => c.GetOrSetAsync(
                        It.IsAny<string>(),
                        It.IsAny<Func<Task<Flight>>>(),
                        It.IsAny<TimeSpan>()))
                    .ReturnsAsync((Flight?)null);

                // Act
                var result = await _sut.GetBookingAsync(flightId, 1);

                // Assert
                result.IsSuccess.Should().BeFalse();
                result.Error.Code.Should().Be("FLIGHT_001");
            }

            [Fact]
            public async Task Should_ReturnFailure_WhenNotEnoughSeats()
            {
                // Arrange
                var flightId = _fixture.Create<int>();
                var ticketCount = 10;
                var flight = _fixture.Build<Flight>().With(f => f.Id, flightId).Create();
                var availableSeatsCount = 5;

                _cacheServiceMock
                    .Setup(c => c.GetOrSetAsync(
                        It.IsAny<string>(),
                        It.IsAny<Func<Task<Flight>>>(),
                        It.IsAny<TimeSpan>()))
                    .ReturnsAsync(flight);

                GetRepositoryMock<FlightSeat>()
                    .Setup(r => r.CountAsync(It.IsAny<ISpecification<FlightSeat>>()))
                    .ReturnsAsync(availableSeatsCount);

                // Act
                var result = await _sut.GetBookingAsync(flightId, ticketCount);

                // Assert
                result.IsSuccess.Should().BeFalse();
                result.Error.Code.Should().Be("SEATS_001");
            }
        }

        #endregion

        #region CreateBookingAsync

        public class CreateBookingAsyncTests : BookingServiceTests
        {
            private readonly Mock<IDbContextTransaction> _transactionMock;

            public CreateBookingAsyncTests()
            {
                _transactionMock = new Mock<IDbContextTransaction>();
                _unitOfWorkMock
                    .Setup(u => u.BeginTransactionAsync())
                    .ReturnsAsync(_transactionMock.Object);
            }

            [Fact]
            public async Task Should_ReturnSuccess_WithBookingId_WhenAllStepsSucceed()
            {
                // Arrange
                var userId = _fixture.Create<string>();
                var flightId = _fixture.Create<int>();
                var seatId1 = _fixture.Create<int>();
                var seatId2 = _fixture.Create<int>();

                var model = _fixture.Build<BookingCreateViewModel>()
                    .With(m => m.FlightId, flightId)
                    .With(m => m.Tickets, new List<TicketBookingVM>
                    {
                         new() { SelectedSeatId = seatId1, TicketPrice = 100, FirstName = "A", LastName = "B", PassportNumber = "P1" },
                         new() { SelectedSeatId = seatId2, TicketPrice = 150, FirstName = "C", LastName = "D", PassportNumber = "P2" }
                    })
                    .Create();

                var availableSeats = new List<FlightSeat>
                {
                    _fixture.Build<FlightSeat>().With(s => s.Id, seatId1).With(s => s.IsAvailable, true).Create(),
                    _fixture.Build<FlightSeat>().With(s => s.Id, seatId2).With(s => s.IsAvailable, true).Create()
                };

                var flightSeatRepo = GetRepositoryMock<FlightSeat>();
                flightSeatRepo
                    .Setup(r => r.GetAllWithSpecAsync(
                        It.IsAny<ISpecification<FlightSeat>>(),
                        It.IsAny<CancellationToken>()))
                    .ReturnsAsync(availableSeats);
                flightSeatRepo
                    .Setup(r => r.Update(It.IsAny<FlightSeat>()));

                _unitOfWorkMock
                    .Setup(u => u.CompleteAsync())
                    .ReturnsAsync(1);

                var bookingId = _fixture.Create<int>();

                var bookingRepo = GetRepositoryMock<Booking>();
                bookingRepo
                    .Setup(r => r.AddAsync(It.IsAny<Booking>()))
                     .Callback<Booking>(b => b.Id = bookingId)
                    .Returns(Task.CompletedTask);

                var ticketRepo = GetRepositoryMock<Ticket>();
                ticketRepo
                    .Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<Ticket>>()))
                    .Returns(Task.CompletedTask);

                var ticketAddOnsRepo = GetRepositoryMock<TicketAddOns>();
                ticketAddOnsRepo
                    .Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<TicketAddOns>>()))
                    .Returns(Task.CompletedTask);

                _cacheServiceMock
                    .Setup(c => c.RemoveAsync(It.IsAny<string>()))
                    .Returns(Task.CompletedTask);

                // Act
                var result = await _sut.CreateBookingAsync(model, userId);

                // Assert
                result.IsSuccess.Should().BeTrue();
                result.Value.Should().BeGreaterThan(0);

                _clientProxyMock.Verify(
                    x => x.SendCoreAsync(
                        "ReceiveUpdate",
                        It.IsAny<object[]>(),
                        It.IsAny<CancellationToken>()
                    ),
                    Times.Once
                );

                _unitOfWorkMock.Verify(u => u.CompleteAsync(), Times.AtLeastOnce);
                _transactionMock.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
                _cacheServiceMock.Verify(c => c.RemoveAsync($"flight:{flightId}"), Times.Once);
                _cacheServiceMock.Verify(c => c.RemoveAsync($"flight:{flightId}:availableseats"), Times.Once);
                _cacheServiceMock.Verify(c => c.RemoveAsync($"user:{userId}:bookings"), Times.Once);
            }

            [Fact]
            public async Task Should_ReturnFailure_WhenTicketsListIsNull()
            {
                // Arrange
                var model = _fixture.Build<BookingCreateViewModel>()
                    .With(m => m.Tickets, (List<TicketBookingVM>?)null)
                    .Create();

                // Act
                var result = await _sut.CreateBookingAsync(model, _fixture.Create<string>());

                // Assert
                result.IsSuccess.Should().BeFalse();
                result.Error.Code.Should().Be("VALIDATION_001");
            }

            [Fact]
            public async Task Should_ReturnFailure_WhenTicketsListIsEmpty()
            {
                // Arrange
                var model = _fixture.Build<BookingCreateViewModel>()
                    .With(m => m.Tickets, new List<TicketBookingVM>())
                    .Create();

                // Act
                var result = await _sut.CreateBookingAsync(model, _fixture.Create<string>());

                // Assert
                result.IsSuccess.Should().BeFalse();
                result.Error.Code.Should().Be("VALIDATION_002");
            }

            [Fact]
            public async Task Should_ReturnFailure_WhenTicketPriceIsZeroOrNegative()
            {
                // Arrange
                var model = _fixture.Build<BookingCreateViewModel>()
                    .With(m => m.Tickets, new List<TicketBookingVM>
                    {
                        new() { SelectedSeatId = 1, TicketPrice = 0 }
                    })
                    .Create();

                // Act
                var result = await _sut.CreateBookingAsync(model, _fixture.Create<string>());

                // Assert
                result.IsSuccess.Should().BeFalse();
                result.Error.Code.Should().Be("VALIDATION_003");
            }

            [Fact]
            public async Task Should_ReturnFailure_WhenSeatsAreNoLongerAvailable()
            {
                // Arrange
                var model = _fixture.Build<BookingCreateViewModel>()
                    .With(m => m.Tickets, new List<TicketBookingVM>
                    {
                new() { SelectedSeatId = 1, TicketPrice = 100 }
                    })
                    .Create();

                var flightSeatRepo = GetRepositoryMock<FlightSeat>();
                flightSeatRepo
                    .Setup(r => r.GetAllWithSpecAsync(
                        It.IsAny<ISpecification<FlightSeat>>(),
                        It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new List<FlightSeat>()); // fewer seats than requested

                // Act
                var result = await _sut.CreateBookingAsync(model, _fixture.Create<string>());

                // Assert
                result.IsSuccess.Should().BeFalse();
                result.Error.Code.Should().Be("SEAT_001");
            }

            [Fact]
            public async Task Should_RollbackTransaction_And_ReleaseSeats_WhenExceptionOccurs()
            {
                // Arrange
                var userId = _fixture.Create<string>();
                var flightId = _fixture.Create<int>();
                var seatId =_fixture.Create<int>();
                var model = _fixture.Build<BookingCreateViewModel>()
                    .With(m => m.FlightId, flightId)
                    .With(m => m.Tickets, new List<TicketBookingVM>
                    {
                        new() { SelectedSeatId = seatId, TicketPrice = 100 }
                    })
                    .Create();

                var availableSeats = new List<FlightSeat>
                {
                    _fixture.Build<FlightSeat>()
                    .With(s => s.Id, seatId)
                    .With(s => s.FlightId, flightId)
                    .With(s => s.IsAvailable, true)
                    .Create()
                };

                var flightSeatRepo = GetRepositoryMock<FlightSeat>();
                flightSeatRepo
                    .Setup(r => r.GetAllWithSpecAsync(
                        It.IsAny<ISpecification<FlightSeat>>(),
                        It.IsAny<CancellationToken>()))
                    .ReturnsAsync(availableSeats);

                var updatedSeats = new List<FlightSeat>();
                flightSeatRepo
                    .Setup(r => r.Update(It.IsAny<FlightSeat>()))
                    .Callback<FlightSeat>(seat => updatedSeats.Add(seat));

                // success first CompleteAsync and fail other
                _unitOfWorkMock
                    .SetupSequence(u => u.CompleteAsync())
                    .ReturnsAsync(1)  // ReserveSeatsAtomicallyAsync
                    .ThrowsAsync(new Exception("Database error"))
                    .ReturnsAsync(1);   // ReleaseSeatsAtomicAsync

                _cacheServiceMock
                    .Setup(c => c.RemoveAsync(It.IsAny<string>()))
                    .Returns(Task.CompletedTask);

                // Act
                var result = await _sut.CreateBookingAsync(model, userId);

                // Assert
                result.IsSuccess.Should().BeFalse();
                result.Error.Code.Should().Be("BOOKING_002");

                _transactionMock.Verify(t => t.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
                flightSeatRepo.Verify(r => r.Update(It.IsAny<FlightSeat>()), Times.Exactly(2));
                updatedSeats.Should().HaveCount(2);
                updatedSeats[0].Id.Should().Be(seatId);
                updatedSeats[0].IsAvailable.Should().BeTrue();
                _cacheServiceMock.Verify(c => c.RemoveAsync($"flight:{flightId}"), Times.Once);
                _cacheServiceMock.Verify(c => c.RemoveAsync($"flight:{flightId}:availableseats"), Times.Once);
            }
        }

        #endregion

        #region CreatePaymentAsync

        public class CreatePaymentAsyncTests : BookingServiceTests
        {
            [Fact]
            public async Task Should_ReturnSuccess_WithPayment()
            {
                // Arrange
                var bookingId = _fixture.Create<int>();
                var totalAmount = 250.75m;

                var paymentRepo = GetRepositoryMock<Payment>();
                paymentRepo
                    .Setup(r => r.AddAsync(It.IsAny<Payment>()))
                    .Returns(Task.CompletedTask);

                _unitOfWorkMock
                    .Setup(u => u.CompleteAsync())
                    .ReturnsAsync(1);

                // Act
                var result = await _sut.CreatePaymentAsync(bookingId, totalAmount);

                // Assert
                result.IsSuccess.Should().BeTrue();
                result.Value.Should().NotBeNull();
                result.Value.BookingID.Should().Be(bookingId);
                result.Value.Amount.Should().Be((long)(totalAmount * 100));
                result.Value.PaymentStatus.Should().Be(PaymentStatus.Pending);
            }

            [Fact]
            public async Task Should_ReturnFailure_WhenExceptionThrown()
            {
                // Arrange
                var paymentRepo = GetRepositoryMock<Payment>();
                paymentRepo
                    .Setup(r => r.AddAsync(It.IsAny<Payment>()))
                    .ThrowsAsync(new Exception());

                // Act
                var result = await _sut.CreatePaymentAsync(1, 100);

                // Assert
                result.IsSuccess.Should().BeFalse();
                result.Error.Code.Should().Be("PAYMENT_001");
            }
        }

        #endregion

        #region CreateStripeSessionAsync

        public class CreateStripeSessionAsyncTests : BookingServiceTests
        {
            [Fact]
            public async Task Should_ReturnSuccess_WithSessionUrl()
            {
                // Arrange
                var payment = _fixture.Create<Payment>();
                var model = _fixture.Build<BookingCreateViewModel>()
                    .With(m => m.Tickets, new List<TicketBookingVM>
                    {
                new() { TicketPrice = 100, SelectedSeatId = 1, FirstName = "A", LastName = "B" },
                new() { TicketPrice = 150, SelectedSeatId = 2, FirstName = "C", LastName = "D" }
                    })
                    .Create();
                var successUrl = "https://success.com";
                var cancelUrl = "https://cancel.com";

                var session = new Session
                {
                    Id = "cs_test_123",
                    Url = "https://checkout.stripe.com/pay/cs_test_123"
                };

                _stripeSessionMock
                    .Setup(s => s.Create(It.IsAny<SessionCreateOptions>()))
                    .Returns(session);

                var paymentRepo = GetRepositoryMock<Payment>();
                paymentRepo
                    .Setup(r => r.Update(It.IsAny<Payment>()));

                _unitOfWorkMock
                    .Setup(u => u.CompleteAsync())
                    .ReturnsAsync(1);

                // Act
                var result = await _sut.CreateStripeSessionAsync(payment, model, successUrl, cancelUrl);

                // Assert
                result.IsSuccess.Should().BeTrue();
                result.Value.Should().Be(session.Url);

                _stripeSessionMock.Verify(s => s.Create(It.Is<SessionCreateOptions>(o =>
                    o.SuccessUrl == successUrl &&
                    o.CancelUrl == cancelUrl &&
                    o.LineItems.Count == model.Tickets.Count)), Times.Once);

                paymentRepo.Verify(r => r.Update(It.Is<Payment>(p =>
                    p.SessionId == session.Id)), Times.Once);
            }

            [Fact]
            public async Task Should_ReturnFailure_WhenStripeThrowsException()
            {
                // Arrange
                _stripeSessionMock
                    .Setup(s => s.Create(It.IsAny<SessionCreateOptions>()))
                    .Throws(new StripeException("Stripe error"));

                // Act
                var result = await _sut.CreateStripeSessionAsync(
                    _fixture.Create<Payment>(),
                    _fixture.Create<BookingCreateViewModel>(),
                    "url", "url");

                // Assert
                result.IsSuccess.Should().BeFalse();
                result.Error.Code.Should().Be("STRIPE_001");
            }
        }

        #endregion

        #region ConfirmPaymentAsync

        public class ConfirmPaymentAsyncTests : BookingServiceTests
        {
            [Fact]
            public async Task Should_ReturnSuccess_WithBooking_WhenPaymentIsPaid()
            {
                // Arrange
                var paymentId = _fixture.Create<int>();
                var userId = _fixture.Create<string>();
                var sessionId = "cs_test_123";
                var paymentIntentId = "pi_123";

                var payment = _fixture.Build<Payment>()
                    .With(p => p.Id, paymentId)
                    .With(p => p.SessionId, sessionId)
                    .Create();

                var booking = _fixture.Build<Booking>()
                    .With(b => b.Id, payment.BookingID)
                    .With(b => b.UserId, userId)
                    .With(b => b.Status, Status.Pending)
                    .Create();

                var session = new Session
                {
                    PaymentStatus = "paid",
                    PaymentIntentId = paymentIntentId
                };

                var paymentRepo = GetRepositoryMock<Payment>();
                paymentRepo
                    .Setup(r => r.GetByIdAsync(paymentId))
                    .ReturnsAsync(payment);

                _stripeSessionMock
                    .Setup(s => s.GetAsync(sessionId))
                    .ReturnsAsync(session);

                var bookingRepo = GetRepositoryMock<Booking>();
                bookingRepo
                    .Setup(r => r.GetByIdAsync(payment.BookingID))
                    .ReturnsAsync(booking);

                _unitOfWorkMock
                    .Setup(u => u.CompleteAsync())
                    .ReturnsAsync(1);

                _cacheServiceMock
                    .Setup(c => c.RemoveAsync(It.IsAny<string>()))
                    .Returns(Task.CompletedTask);

                // Mock GetBookingWithDetailsAsync dependency
                var detailedBooking = _fixture.Build<Booking>()
                    .With(b => b.Id, booking.Id)
                    .With(b => b.AppUser, _fixture.Build<AppUser>().With(u => u.Email, "test@test.com").Create())
                    .Create();

                var bookingSpecRepo = GetRepositoryMock<Booking>();
                bookingSpecRepo
                    .Setup(r => r.GetEntityWithSpecAsync(It.IsAny<ISpecification<Booking>>()))
                    .ReturnsAsync(detailedBooking);

                // Act
                var result = await _sut.ConfirmPaymentAsync(paymentId, userId);

                // Assert
                result.IsSuccess.Should().BeTrue();
                result.Value.Should().Be(detailedBooking);
                booking.Status.Should().Be(Status.Confirmed);
                payment.PaymentStatus.Should().Be(PaymentStatus.Approved);
                payment.PaymentIntentId.Should().Be(paymentIntentId);
            }

            [Fact]
            public async Task Should_ReturnFailure_WhenPaymentNotFound()
            {
                // Arrange
                var paymentRepo = GetRepositoryMock<Payment>();
                paymentRepo
                    .Setup(r => r.GetByIdAsync(It.IsAny<int>()))
                    .ReturnsAsync((Payment?)null);

                // Act
                var result = await _sut.ConfirmPaymentAsync(1, _fixture.Create<string>());

                // Assert
                result.IsSuccess.Should().BeFalse();
                result.Error.Code.Should().Be("PAYMENT_001");
            }

            [Fact]
            public async Task Should_ReturnFailure_WhenPaymentNotCompleted()
            {
                // Arrange
                var payment = _fixture.Create<Payment>();
                GetRepositoryMock<Payment>()
                    .Setup(r => r.GetByIdAsync(payment.Id))
                    .ReturnsAsync(payment);

                _stripeSessionMock
                    .Setup(s => s.GetAsync(payment.SessionId))
                    .ReturnsAsync(new Session { PaymentStatus = "unpaid" });

                // Act
                var result = await _sut.ConfirmPaymentAsync(payment.Id, _fixture.Create<string>());

                // Assert
                result.IsSuccess.Should().BeFalse();
                result.Error.Code.Should().Be("PAYMENT_002");
            }

            [Fact]
            public async Task Should_ReturnFailure_WhenBookingDoesNotBelongToUser()
            {
                // Arrange
                var payment = _fixture.Create<Payment>();
                GetRepositoryMock<Payment>()
                    .Setup(r => r.GetByIdAsync(payment.Id))
                    .ReturnsAsync(payment);

                _stripeSessionMock
                    .Setup(s => s.GetAsync(payment.SessionId))
                    .ReturnsAsync(new Session { PaymentStatus = "paid" });

                var booking = _fixture.Build<Booking>()
                    .With(b => b.Id, payment.BookingID)
                    .With(b => b.UserId, "different-user")
                    .Create();
                GetRepositoryMock<Booking>()
                    .Setup(r => r.GetByIdAsync(payment.BookingID))
                    .ReturnsAsync(booking);

                // Act
                var result = await _sut.ConfirmPaymentAsync(payment.Id, "correct-user");

                // Assert
                result.IsSuccess.Should().BeFalse();
                result.Error.Code.Should().Be("BOOKING_003");
            }
        }

        #endregion

        #region CancelBookingAsync

        public class CancelBookingAsyncTests : BookingServiceTests
        {
            [Fact]
            public async Task Should_ReturnSuccess_WhenCancellationSucceeds()
            {
                // Arrange
                var paymentId = _fixture.Create<int>();
                var flightId = _fixture.Create<int>();
                var booking = _fixture.Build<Booking>()
                    .With(b => b.FlightId, flightId)
                    .Create();
                var tickets = _fixture.Build<Ticket>()
                    .With(t => t.FlightSeat, _fixture.Build<FlightSeat>().Create())
                    .With(t => t.TicketAddOns, _fixture.CreateMany<TicketAddOns>().ToList())
                    .CreateMany(2).ToList();
                booking.Tickets = tickets;

                var payment = _fixture.Build<Payment>()
                    .With(p => p.Id, paymentId)
                    .With(p => p.Booking, booking)
                    .Create();

                var paymentRepo = GetRepositoryMock<Payment>();
                paymentRepo
                    .Setup(r => r.GetEntityWithSpecAsync(It.IsAny<ISpecification<Payment>>()))
                    .ReturnsAsync(payment);

                var flightSeatRepo = GetRepositoryMock<FlightSeat>();
                var ticketAddOnsRepo = GetRepositoryMock<TicketAddOns>();

                _unitOfWorkMock
                    .Setup(u => u.CompleteAsync())
                    .ReturnsAsync(1);

                var capturedMethods = new List<string>();
                _clientProxyMock.Setup(x => x.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
                    .Callback<string, object[], CancellationToken>((method, args, ct) => capturedMethods.Add(method))
                    .Returns(Task.CompletedTask);

                // Act
                var result = await _sut.CancelBookingAsync(paymentId);

                // Assert
                result.IsSuccess.Should().BeTrue();
                result.Value.Should().BeTrue();

                capturedMethods.Should().Contain("ReceiveUpdate");

                flightSeatRepo.Verify(r => r.Update(It.Is<FlightSeat>(s =>
                    s.IsAvailable == true && s.TicketId == null)), Times.Exactly(tickets.Count));
                ticketAddOnsRepo.Verify(r => r.RemoveRange(It.IsAny<IEnumerable<TicketAddOns>>()), Times.Exactly(tickets.Count));
                paymentRepo.Verify(r => r.Delete(payment), Times.Once);
                _cacheServiceMock.Verify(c => c.RemoveAsync($"flight:{flightId}"), Times.Once);
                _cacheServiceMock.Verify(c => c.RemoveAsync($"flight:{flightId}:availableseats"), Times.Once);
            }

            [Fact]
            public async Task Should_ReturnFailure_WhenPaymentNotFound()
            {
                // Arrange
                var paymentRepo = GetRepositoryMock<Payment>();
                paymentRepo
                    .Setup(r => r.GetEntityWithSpecAsync(It.IsAny<ISpecification<Payment>>()))
                    .ReturnsAsync((Payment?)null);

                // Act
                var result = await _sut.CancelBookingAsync(1);

                // Assert
                result.IsSuccess.Should().BeFalse();
                result.Error.Code.Should().Be("PAYMENT_003");
            }

            [Fact]
            public async Task Should_ReturnFailure_WhenExceptionThrown()
            {
                // Arrange
                var paymentRepo = GetRepositoryMock<Payment>();
                paymentRepo
                    .Setup(r => r.GetEntityWithSpecAsync(It.IsAny<ISpecification<Payment>>()))
                    .ThrowsAsync(new Exception());

                // Act
                var result = await _sut.CancelBookingAsync(1);

                // Assert
                result.IsSuccess.Should().BeFalse();
                result.Error.Code.Should().Be("CANCEL_001");
            }
        }

        #endregion

        #region GetBookingWithDetailsAsync

        public class GetBookingWithDetailsAsyncTests : BookingServiceTests
        {
            [Fact]
            public async Task Should_ReturnBooking_FromCache_WhenCacheHit()
            {
                // Arrange
                var bookingId = _fixture.Create<int>();
                var userId = _fixture.Create<string>();
                var cacheKey = $"booking:{bookingId}:details:{userId}";
                var cachedBooking = _fixture.Build<Booking>()
                    .With(b => b.AppUser, _fixture.Build<AppUser>().With(u => u.Email, "test@test.com").Create())
                    .Create();

                _cacheServiceMock
                    .Setup(c => c.GetAsync<Booking>(cacheKey))
                    .ReturnsAsync(cachedBooking);

                // Act
                var result = await _sut.GetBookingWithDetailsAsync(bookingId, userId);

                // Assert
                result.IsSuccess.Should().BeTrue();
                result.Value.Should().Be(cachedBooking);
                GetRepositoryMock<Booking>().Verify(r => r.GetEntityWithSpecAsync(It.IsAny<ISpecification<Booking>>()), Times.Never);
            }

            [Fact]
            public async Task Should_ReturnBooking_FromDatabase_WhenCacheMiss()
            {
                // Arrange
                var bookingId = _fixture.Create<int>();
                var userId = _fixture.Create<string>();
                var cacheKey = $"booking:{bookingId}:details:{userId}";

                _cacheServiceMock.Setup(c => c.GetAsync<Booking>(cacheKey))
                    .ReturnsAsync((Booking?)null);

                var bookingFromDb = _fixture.Build<Booking>()
                    .With(b => b.Id, bookingId)
                    .With(b => b.UserId, userId)
                    .With(b => b.AppUser, _fixture.Build<AppUser>().With(u => u.Email, "test@test.com").Create())
                    .Create();

                var bookingRepo = GetRepositoryMock<Booking>();
                bookingRepo
                    .Setup(r => r.GetEntityWithSpecAsync(It.IsAny<ISpecification<Booking>>()))
                    .ReturnsAsync(bookingFromDb);

                _cacheServiceMock
                    .Setup(c => c.SetAsync(cacheKey, bookingFromDb, It.IsAny<TimeSpan>()))
                    .Returns(Task.CompletedTask);

                // Act
                var result = await _sut.GetBookingWithDetailsAsync(bookingId, userId);

                // Assert
                result.IsSuccess.Should().BeTrue();
                result.Value.Should().Be(bookingFromDb);
                _cacheServiceMock.Verify(c => c.SetAsync(cacheKey, bookingFromDb, It.IsAny<TimeSpan>()), Times.Once);
            }

            [Fact]
            public async Task Should_ReturnFailure_WhenBookingNotFoundOrAccessDenied()
            {
                // Arrange
                var bookingId = _fixture.Create<int>();
                var userId = _fixture.Create<string>();

                _cacheServiceMock.Setup(c => c.GetAsync<Booking>(It.IsAny<string>()))
                    .ReturnsAsync((Booking?)null);

                GetRepositoryMock<Booking>()
                    .Setup(r => r.GetEntityWithSpecAsync(It.IsAny<ISpecification<Booking>>()))
                    .ReturnsAsync((Booking?)null);

                // Act
                var result = await _sut.GetBookingWithDetailsAsync(bookingId, userId);

                // Assert
                result.IsSuccess.Should().BeFalse();
                result.Error.Code.Should().Be("BOOKING_004");
            }
        }

        #endregion

    }
}

