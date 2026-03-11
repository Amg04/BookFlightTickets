using BookFlightTickets.Core.Domain.Entities;
using BookFlightTickets.Core.Domain.Hubs;
using BookFlightTickets.Core.Domain.RepositoryContracts;
using BookFlightTickets.Core.Domain.ResultPattern;
using BookFlightTickets.Core.Domain.Specifications;
using BookFlightTickets.Core.ServiceContracts;
using BookFlightTickets.Core.Shared.Utility;
using BookFlightTickets.Core.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Stripe.Checkout;

namespace BookFlightTickets.Core.Services
{
    public class BookingService : IBookingService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<BookingService> _logger;
        private readonly IRedisCacheService _cacheService;
        private readonly IStripeSessionService _stripeSessionService;
        private readonly IHubContext<DashboardHub> _hubContext;

        public BookingService(
            IUnitOfWork unitOfWork,
            ILogger<BookingService> logger,
            IRedisCacheService cacheService,
            IStripeSessionService stripeSessionService,
            IHubContext<DashboardHub> hubContext)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _cacheService = cacheService;
            _stripeSessionService = stripeSessionService;
            _hubContext = hubContext;
        }

        #region GetBookingAsync

        public async Task<Result<BookingCreateViewModel>> GetBookingAsync(int flightId, int ticketCount)
        {
            _logger.LogInformation("Getting booking view model for FlightId: {FlightId}, TicketCount: {TicketCount}", flightId, ticketCount);

            var flightCacheKey = $"flight:{flightId}";
            var seatsCacheKey = $"flight:{flightId}:availableseats";
            var addOnsCacheKey = "addons:all";

            var flight = await _cacheService.GetOrSetAsync(
                flightCacheKey,
                async () =>
                {
                    var spec = new BaseSpecification<Flight>(f => f.Id == flightId);
                    spec.Includes.Add(f => f.Airline);
                    spec.Includes.Add(f => f.Airplane);
                    spec.Includes.Add(f => f.DepartureAirport);
                    spec.Includes.Add(f => f.ArrivalAirport);
                    return await _unitOfWork.Repository<Flight>().GetEntityWithSpecAsync(spec);
                }, TimeSpan.FromHours(2)
            );

            if (flight == null)
            {
                _logger.LogWarning("Flight not found. FlightId: {FlightId}", flightId);
                return Result<BookingCreateViewModel>.Failure(new Error("FLIGHT_001", "Flight not found"));
            }

            var seatCountSpec = new BaseSpecification<FlightSeat>(s => s.FlightId == flight.Id && s.IsAvailable);
            var availableSeatsCount = await _unitOfWork.Repository<FlightSeat>().CountAsync(seatCountSpec);

            if (ticketCount > availableSeatsCount)
            {
                _logger.LogWarning("Not enough seats. Available: {AvailableSeats}, Requested: {TicketCount}", availableSeatsCount, ticketCount);
                return Result<BookingCreateViewModel>.Failure(new Error("SEATS_001", "Not enough available seats"));
            }

            var seatDetailsSpec = new BaseSpecification<FlightSeat>(s => s.FlightId == flight.Id && s.IsAvailable);
            seatDetailsSpec.Includes.Add(f => f.Seat);
            var flightSeats = await _unitOfWork.Repository<FlightSeat>().GetAllWithSpecAsync(seatDetailsSpec);
            var seats = flightSeats.Where(s => s.Seat != null).Select(s => new SelectListItem
            {
                Value = s.Id.ToString(),
                Text = $"{s.Seat.Row}{s.Seat.Number} ({s.Seat.Class}) {s.Seat.Price}$"
            }).ToList();

            List<AddOn> addOns;
            var cachedAddOns = await _cacheService.GetAsync<List<AddOn>>(addOnsCacheKey);

            if (cachedAddOns is not null)
            {
                addOns = cachedAddOns;
                _logger.LogDebug("AddOns retrieved from cacheService. Count: {Count}", addOns.Count);
            }
            else
            {
                addOns = (await _unitOfWork.Repository<AddOn>().GetAllAsync()).ToList();
                await _cacheService.SetAsync(addOnsCacheKey, addOns, TimeSpan.FromHours(24));
                _logger.LogDebug("AddOns cached. Count: {Count}", addOns.Count);
            }

            _logger.LogInformation("Booking view model created successfully for FlightId: {FlightId}", flightId);
            return Result<BookingCreateViewModel>.Success(new BookingCreateViewModel
            {
                FlightId = flight.Id,
                Flight = flight,
                AvailableSeats = seats,
                AddOns = addOns,
                Tickets = Enumerable.Range(0, ticketCount)
                    .Select(_ => new TicketBookingVM()).ToList()
            });
        }

        #endregion

        #region CreateBookingAsync

        public async Task<Result<int>> CreateBookingAsync(BookingCreateViewModel model, string userId)
        {
            _logger.LogInformation("Creating booking for FlightId: {FlightId}, UserId: {UserId}, Tickets: {TicketCount}", model.FlightId, userId, model.Tickets?.Count);

            if (model.Tickets == null)
            {
                _logger.LogError("Tickets list is null for FlightId: {FlightId}", model.FlightId);
                return Result<int>.Failure(new Error("VALIDATION_001", "Tickets list is null"));
            }

            if (model.Tickets.Count == 0)
            {
                _logger.LogError("No tickets provided for FlightId: {FlightId}", model.FlightId);
                return Result<int>.Failure(new Error("VALIDATION_002", "No tickets provided"));
            }

            if (!model.Tickets.All(t => t.TicketPrice > 0))
            {
                _logger.LogError("Invalid ticket prices for FlightId: {FlightId}", model.FlightId);
                return Result<int>.Failure(new Error("VALIDATION_003", "All tickets must have Price > 0"));
            }

            var bookedSeatsResult = await ReserveSeatsAtomicallyAsync(model.Tickets);
            if (!bookedSeatsResult.IsSuccess)
                return Result<int>.Failure(bookedSeatsResult.Error!);

            var bookedSeats = bookedSeatsResult.Value!;

            await using var transaction = await _unitOfWork.BeginTransactionAsync();

            try
            {
                decimal totalPrice = model.Tickets.Sum(ticket => ticket.TicketPrice);
                string PNR = UniqueNumberGenerator.Generate();
                _logger.LogDebug("Total Price: {TotalPrice}, PNR: {PNR}", totalPrice, PNR);

                var booking = new Booking {
                    UserId = userId,
                    FlightId = model.FlightId,
                    BookingDate = DateTime.UtcNow,
                    PNR = PNR,
                    TotalPrice = totalPrice,
                    Status = Status.Pending,
                    LastUpdated = DateTime.UtcNow,
                };

                await _unitOfWork.Repository<Booking>().AddAsync(booking);
                await _unitOfWork.CompleteAsync();

                await CreateTicketsAndAddonsAtomicAsync(booking.Id, model.Tickets, bookedSeatsResult.Value);
                await transaction.CommitAsync();
                
                try
                {
                    await _hubContext.Clients.Group(SD.Admin).SendAsync("ReceiveUpdate");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send real-time update to admin group");
                }

                _logger.LogInformation("Booking completed successfully. BookingId: {BookingId}", booking.Id);

                await RemoveAffectedFlightCachesAsync(model.FlightId);
                await _cacheService.RemoveAsync($"user:{userId}:bookings");
                _logger.LogInformation("Booking created: {BookingId}", booking.Id);

                return Result<int>.Success(booking.Id);
            }
            catch(Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to create booking. FlightId: {FlightId}, UserId: {UserId}", model.FlightId, userId);
                await ReleaseSeatsAtomicAsync(bookedSeatsResult.Value);
                return Result<int>.Failure(new Error("BOOKING_002", "Failed to create booking", ex.Message));
            }
        }

        private async Task CreateTicketsAndAddonsAtomicAsync(
           int bookingId,
           List<TicketBookingVM> tickets,
           Dictionary<int, FlightSeat> bookedSeats)
        {
            var allTickets = tickets.Select((t, i) => new Ticket
            {
                BookingID = bookingId,
                TicketNumber = UniqueNumberGenerator.Generate(),
                FlightSeatId = t.SelectedSeatId,
                FirstName = t.FirstName,
                LastName = t.LastName,
                PassportNumber = t.PassportNumber,
                TicketPrice = t.TicketPrice,
            }).ToList();

            await _unitOfWork.Repository<Ticket>().AddRangeAsync(allTickets);
            await _unitOfWork.CompleteAsync();

            var addOnsBatch = new List<TicketAddOns>();
            foreach (var ticket in allTickets)
            {
                if (bookedSeats.TryGetValue(ticket.FlightSeatId, out var seat))
                {
                    seat.TicketId = ticket.Id;
                    _unitOfWork.Repository<FlightSeat>().Update(seat);
                }

                var ticketVM = tickets.First(t => t.SelectedSeatId == ticket.FlightSeatId);
                addOnsBatch.AddRange(ticketVM.SelectedAddOnIds.Select(aoId => new TicketAddOns
                {
                    TicketId = ticket.Id,
                    AddOnID = aoId
                }));
            }

            if (addOnsBatch.Any())
                await _unitOfWork.Repository<TicketAddOns>().AddRangeAsync(addOnsBatch);

            await _unitOfWork.CompleteAsync();
        }

        private async Task<Result<Dictionary<int, FlightSeat>>> ReserveSeatsAtomicallyAsync(List<TicketBookingVM> tickets)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            var seatIds = tickets.Where(t => t.SelectedSeatId > 0).Select(t => t.SelectedSeatId).ToList();
            var spec = new BaseSpecification<FlightSeat>(fs => seatIds.Contains(fs.Id) && fs.IsAvailable);
            spec.OrderByAsc(s => s.Id);

            var seats = await _unitOfWork.Repository<FlightSeat>().GetAllWithSpecAsync(spec, cts.Token);

            if (seats.Count() != seatIds.Count())
                return Result<Dictionary<int, FlightSeat>>.Failure(new Error("SEAT_001", "Seats no longer available"));

            foreach (var seat in seats)
            {
                seat.IsAvailable = false;
                _unitOfWork.Repository<FlightSeat>().Update(seat);
            }
            await _unitOfWork.CompleteAsync();

            return Result<Dictionary<int, FlightSeat>>.Success(seats.ToDictionary(s => s.Id));
        }

        private async Task ReleaseSeatsAtomicAsync(Dictionary<int, FlightSeat> bookedSeats)
        {
            _logger.LogInformation("Releasing {SeatCount} seats", bookedSeats.Count);
            foreach (var kvp in bookedSeats)
            {
                var flightSeat = kvp.Value;
                flightSeat.IsAvailable = true;
                flightSeat.TicketId = null;
                _unitOfWork.Repository<FlightSeat>().Update(flightSeat);
                _logger.LogDebug("Seat released. SeatId: {SeatId}", kvp.Key);
            }
            await _unitOfWork.CompleteAsync();

            if (bookedSeats.Any())
            {
                var flightId = bookedSeats.First().Value.FlightId;
                await RemoveAffectedFlightCachesAsync(flightId);
                _logger.LogDebug("Cleared available seats cacheService after release for FlightId: {FlightId}", flightId);
            }
        }

        #endregion

        #region CreatePaymentAsync

        public async Task<Result<Payment>> CreatePaymentAsync(int bookingId, decimal totalAmount)
        {
            _logger.LogInformation("Creating payment for BookingId: {BookingId}, Amount: {Amount}", bookingId, totalAmount);
            try
            {
                var payment = new Payment
                {
                    BookingID = bookingId,
                    Amount = (long)(totalAmount * 100),
                    PaymentDate = DateTime.UtcNow,
                    PaymentStatus = PaymentStatus.Pending,
                };

                await _unitOfWork.Repository<Payment>().AddAsync(payment);
                await _unitOfWork.CompleteAsync();
                _logger.LogInformation("Payment created. PaymentId: {PaymentId}", payment.Id);
                return Result<Payment>.Success(payment);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create payment for BookingId: {BookingId}", bookingId);
                return Result<Payment>.Failure(new Error("PAYMENT_001", "Failed to create payment", ex.Message));
            }
        }

        #endregion

        #region CreateStripeSessionAsync

        public async Task<Result<string>> CreateStripeSessionAsync(
            Payment payment, 
            BookingCreateViewModel model,
            string successUrl, 
            string cancelUrl)
        {
            _logger.LogInformation("Creating Stripe session for PaymentId: {PaymentId}, BookingId: {BookingId}", payment.Id, payment.BookingID);
            try
            {
                var options = new SessionCreateOptions
                {
                    PaymentMethodTypes = new List<string> { "card" },
                    LineItems = model.Tickets!.Select((ticket, index) => new SessionLineItemOptions
                    {
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            UnitAmount = (long)(ticket.TicketPrice * 100),
                            Currency = "usd",
                            ProductData = new SessionLineItemPriceDataProductDataOptions
                            {
                                Name = $"Ticket {index + 1} - Flight {model.FlightId}",
                                Description = $"Seat: {ticket.SelectedSeatId} | {ticket.FirstName} {ticket.LastName}"
                            }
                        },
                        Quantity = 1
                    }).ToList(),
                    Mode = "payment",
                    SuccessUrl = successUrl,
                    CancelUrl = cancelUrl
                };

                var session = _stripeSessionService.Create(options);
                payment.SessionId = session.Id;
                _unitOfWork.Repository<Payment>().Update(payment);
                await _unitOfWork.CompleteAsync();
                _logger.LogInformation("Stripe session created. SessionId: {SessionId}, URL: {SessionUrl}", session.Id, session.Url);
                return Result<string>.Success(session.Url!);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create Stripe session for PaymentId: {PaymentId}", payment.Id);
                return Result<string>.Failure(new Error("STRIPE_001", "Failed to create Stripe session", ex.Message));
            }
        }

        #endregion

        #region ConfirmPaymentAsync

        public async Task<Result<Booking>> ConfirmPaymentAsync(int paymentId, string userId)
        {
            _logger.LogInformation("Confirming payment. PaymentId: {PaymentId}, UserId: {UserId}", paymentId, userId);

            var payment = await _unitOfWork.Repository<Payment>().GetByIdAsync(paymentId);
            if (payment == null)
            {
                _logger.LogWarning("Payment not found. PaymentId: {PaymentId}", paymentId);
                return Result<Booking>.Failure(new Error("PAYMENT_001", "Payment not found"));
            }

            var session = await _stripeSessionService.GetAsync(payment.SessionId);

            if (session.PaymentStatus != "paid")
            {
                _logger.LogWarning("Payment not completed. PaymentId: {PaymentId}, Status: {Status}", paymentId, session.PaymentStatus);
                return Result<Booking>.Failure(new Error("PAYMENT_002", "Payment not completed"));
            }

            var booking = await _unitOfWork.Repository<Booking>().GetByIdAsync(payment.BookingID);
            if (booking == null || booking.UserId != userId)
            {
                _logger.LogWarning("Booking not found or access denied. BookingId: {BookingId}, UserId: {UserId}", payment.BookingID, userId);
                return Result<Booking>.Failure(new Error("BOOKING_003", "Booking not found or access denied"));
            }

            booking.Status = Status.Confirmed;
            payment.PaymentStatus = PaymentStatus.Approved;
            payment.PaymentIntentId = session.PaymentIntentId;
            await _unitOfWork.CompleteAsync();

            await _cacheService.RemoveAsync($"booking:{booking.Id}:details:{userId}");
            await _cacheService.RemoveAsync($"user:{userId}:bookings");
            _logger.LogDebug("Cleared booking cacheService after confirmation. BookingId: {BookingId}", booking.Id);

            _logger.LogInformation("Payment confirmed successfully. PaymentId: {PaymentId}, BookingId: {BookingId}", paymentId, booking.Id);

            var bookingResult = await GetBookingWithDetailsAsync(payment.BookingID, userId);
            if (!bookingResult.IsSuccess)
                return bookingResult;

            return Result<Booking>.Success(bookingResult.Value!);
        }

        #endregion

        #region CancelBookingAsync

        public async Task<Result<bool>> CancelBookingAsync(int paymentId)
        {
            _logger.LogInformation("Cancelling booking for PaymentId: {PaymentId}", paymentId);
            try
            {
                var spec = new BaseSpecification<Payment>(p => p.Id == paymentId);
                spec.ComplexIncludes.Add(c => c.Include(t => t.Booking)
                        .ThenInclude(a => a.Tickets)
                        .ThenInclude(d => d.FlightSeat));
                spec.ComplexIncludes.Add(c => c.Include(t => t.Booking)
                        .ThenInclude(a => a.Tickets)
                        .ThenInclude(d => d.TicketAddOns));

                var payment = await _unitOfWork.Repository<Payment>().GetEntityWithSpecAsync(spec);
                if (payment == null)
                {
                    _logger.LogDebug("Payment not found. PaymentId: {PaymentId}", paymentId);
                    return Result<bool>.Failure(new Error("PAYMENT_003", "Payment not found"));
                }

                _logger.LogDebug("Found {TicketCount} tickets to cancel for BookingId: {BookingId}", payment.Booking.Tickets.Count, payment.BookingID);

                foreach (var ticket in payment.Booking.Tickets)
                {
                    if (ticket.TicketAddOns != null && ticket.TicketAddOns.Any())
                    {
                        _logger.LogDebug("Removing {AddOnCount} add-ons for TicketId: {TicketId}", ticket.TicketAddOns.Count, ticket.Id);
                        _unitOfWork.Repository<TicketAddOns>().RemoveRange(ticket.TicketAddOns);
                    }
                    if (ticket.FlightSeat != null)
                    {
                        ticket.FlightSeat.IsAvailable = true;
                        ticket.FlightSeat.TicketId = null;
                        _unitOfWork.Repository<FlightSeat>().Update(ticket.FlightSeat);
                        _logger.LogDebug("Released seat for TicketId: {TicketId}", ticket.Id);
                    }
                }

                await RemoveAffectedFlightCachesAsync(payment.Booking.FlightId);
                _logger.LogInformation("Deleting payment and booking. PaymentId: {PaymentId}", paymentId);

                _unitOfWork.Repository<Payment>().Delete(payment);
                _unitOfWork.Repository<Booking>().Delete(payment.Booking);
                await _unitOfWork.CompleteAsync();

                try
                {
                    await _hubContext.Clients.Group(SD.Admin).SendAsync("ReceiveUpdate");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send real-time update to admin group");
                }

                _logger.LogInformation("Booking cancelled successfully. PaymentId: {PaymentId}", paymentId);
                return Result<bool>.Success(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cancel booking for PaymentId: {PaymentId}", paymentId);
                return Result<bool>.Failure(new Error("CANCEL_001", "Failed to cancel booking", ex.Message));
            }
        }

        #endregion

        #region GetBookingWithDetailsAsync

        public async Task<Result<Booking>> GetBookingWithDetailsAsync(int bookingId, string userId)
        {
            _logger.LogDebug("Getting booking details. BookingId: {BookingId}, UserId: {UserId}", bookingId, userId);

            var cacheKey = $"booking:{bookingId}:details:{userId}";
            var cachedBooking = await _cacheService.GetAsync<Booking>(cacheKey);
            if (cachedBooking is not null)
            {
                _logger.LogDebug("Booking retrieved from cacheService. BookingId: {BookingId}", bookingId);
                return Result<Booking>.Success(cachedBooking);
            }

            var spec = new BaseSpecification<Booking>(b => b.Id == bookingId && b.UserId == userId);
            spec.ComplexIncludes.Add(c => c.Include(t => t.Tickets)
                 .ThenInclude(a => a.TicketAddOns)
                 .ThenInclude(a => a.AddOn));
            spec.ComplexIncludes.Add(c => c.Include(t => t.Flight)
                .ThenInclude(a => a.FlightSeats)
                .ThenInclude(a => a.Seat));
            spec.ComplexIncludes.Add(c => c.Include(t => t.Flight)
                .ThenInclude(a => a.DepartureAirport));
            spec.ComplexIncludes.Add(c => c.Include(t => t.Flight)
                .ThenInclude(a => a.ArrivalAirport));
            spec.ComplexIncludes.Add(c => c.Include(t => t.Flight)
                .ThenInclude(a => a.Airline));
            spec.ComplexIncludes.Add(c => c.Include(t => t.Flight)
                .ThenInclude(a => a.Airplane));
            spec.Includes.Add(b => b.Payment!);
            spec.Includes.Add(b => b.AppUser);

            var booking = await _unitOfWork.Repository<Booking>().GetEntityWithSpecAsync(spec);
            if (booking?.AppUser?.Email == null)
            {
                _logger.LogWarning("Booking not found or access denied. BookingId: {BookingId}, UserId: {UserId}", bookingId, userId);
                return Result<Booking>.Failure(new Error("BOOKING_004", "Booking not found or access denied"));
            }

            await _cacheService.SetAsync(cacheKey, booking, TimeSpan.FromMinutes(15));
            _logger.LogDebug("Booking cached. BookingId: {BookingId}", bookingId);

            _logger.LogInformation("Booking details retrieved successfully. BookingId: {BookingId}", bookingId);
            return Result<Booking>.Success(booking);
        }

        #endregion

        #region Helper methods
        private async Task RemoveAffectedFlightCachesAsync(int flightId)
        {
            try
            {
                var specificCacheKeys = new[]
                {
                    $"flight:{flightId}",
                    $"flight:{flightId}:availableseats"
                };

                foreach (var key in specificCacheKeys)
                {
                    await _cacheService.RemoveAsync(key);
                }

                _logger.LogDebug("Removed affected flight caches for FlightId: {FlightId}", flightId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error removing affected flight caches for FlightId: {FlightId}", flightId);
            }
        }

        #endregion

    }
}
