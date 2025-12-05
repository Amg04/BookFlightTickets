using BLLProject.Interfaces;
using BLLProject.Specifications;
using DAL.models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PL.ViewModels;
using Rotativa.AspNetCore;
using Stripe.Checkout;
using System.Security.Claims;
using Utility;

namespace PL.Areas.Customer.Controllers
{
    [Area(SD.Customer)]
    [Authorize]
    public class BookController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        public BookController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        #region Book

        [HttpGet]
        public async Task<IActionResult> Book(int flightId, int ticketCount)
        {
            var spec = new BaseSpecification<Flight>(f => f.Id == flightId);
            spec.Includes.Add(f => f.Airline);
            spec.Includes.Add(f => f.Airplane);
            spec.Includes.Add(f => f.DepartureAirport);
            spec.Includes.Add(f => f.ArrivalAirport);

            var flight = await _unitOfWork.Repository<Flight>().GetEntityWithSpecAsync(spec);

            if (flight == null)
                return NotFound();

            var seatSpec = new BaseSpecification<FlightSeat>(s => s.FlightId == flight.Id && s.IsAvailable);
            seatSpec.Includes.Add(f => f.Seat);
            var availableSeatsCount = await _unitOfWork.Repository<FlightSeat>().CountAsync(seatSpec);

            if (ticketCount > availableSeatsCount)
            {
                ViewBag.ErrorMessage = $"Cannot book {ticketCount} tickets - Only {availableSeatsCount} seats available. Please book the next flight.";
                ViewBag.FlightId = flightId;
                return View("SeatsNotAvailable"); 
            }
            var flightSeats = await _unitOfWork.Repository<FlightSeat>().GetAllWithSpecAsync(seatSpec);
            var seats = flightSeats.Where(s => s.Seat != null).Select(s => new SelectListItem
            {
                Value = s.Id.ToString(),
                Text = $"{s.Seat.Row}{s.Seat.Number} ({s.Seat.Class}) {s.Seat.Price}$"
            }).ToList();

            var addOns = (await _unitOfWork.Repository<AddOn>().GetAllAsync()).ToList();
            var vm = new BookingCreateViewModel
            {
                FlightId = flight.Id,
                Flight = flight,
                AvailableSeats = seats,
                AddOns = addOns,
                Tickets = Enumerable.Range(0, ticketCount)
                        .Select(_ => new TicketBookingVM())
                        .ToList()
            };

            return View(vm);
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Book(BookingCreateViewModel model)
        {
            if (model.Tickets == null || model.Tickets.Count == 0)
            {
                var seatSpec = new BaseSpecification<FlightSeat>(s => s.FlightId == model.FlightId && s.IsAvailable);
                seatSpec.Includes.Add(fs => fs.Seat);
                var flightSeats = await _unitOfWork.Repository<FlightSeat>().GetAllWithSpecAsync(seatSpec);
                var seats = flightSeats.Select(s => new SelectListItem
                {
                    Value = s.Id.ToString(),
                    Text = $"{s.Seat.Row}{s.Seat.Number} ({s.Seat.Class}) {s.Seat.Price}$"
                }).ToList();

                model.AddOns = (await _unitOfWork.Repository<AddOn>().GetAllAsync()).ToList();
                ModelState.AddModelError("", "No tickets provided.");
                return View(model);
            }
            if (!model.Tickets!.All(t => t.TicketPrice > 0))
            {
                ModelState.AddModelError("", "Please calculate ticket prices before submitting.");
                return View(model);
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            decimal totalPrice = model.Tickets!.Sum(ticket => ticket.TicketPrice);

            string PNR = UniqueNumberGenerator.Generate();
            Dictionary<int, FlightSeat> bookedFlightSeats = new();
            foreach (var ticket in model.Tickets!)
            {
                if (ticket.SelectedSeatId > 0)
                {
                    var flightSeatSpec = new BaseSpecification<FlightSeat>(
                        fs => fs.FlightId == model.FlightId && fs.Id == ticket.SelectedSeatId);
                    var flightSeat = await _unitOfWork.Repository<FlightSeat>().GetEntityWithSpecAsync(flightSeatSpec);
                    if (flightSeat != null)
                    {
                        flightSeat.IsAvailable = false;
                        _unitOfWork.Repository<FlightSeat>().Update(flightSeat);
                        bookedFlightSeats[ticket.SelectedSeatId] = flightSeat;
                    }
                }
            }
            await _unitOfWork.CompleteAsync();

            var book = new Booking
            {
                UserId = userId,
                FlightId = model.FlightId,
                BookingDate = DateTime.UtcNow,
                PNR = PNR,
                TotalPrice = totalPrice,
                Status = Status.Pending,
                LastUpdated = DateTime.UtcNow,
            };

            await _unitOfWork.Repository<Booking>().AddAsync(book);
            await _unitOfWork.CompleteAsync();


            using var transaction = await _unitOfWork.BeginTransactionAsync();

            List<Ticket> allTickets = new();
            Dictionary<int, List<TicketAddOns>> ticketAddOnsMap = new(); 

            for (int i = 0; i < model.Tickets!.Count; i++)
            {
                string ticketNumber = UniqueNumberGenerator.Generate();
                var ticket = new Ticket
                {
                    BookingID = book.Id,
                    TicketNumber = ticketNumber,
                    FlightSeatId = model.Tickets[i].SelectedSeatId,
                    FirstName = model.Tickets[i].FirstName,
                    LastName = model.Tickets[i].LastName,
                    PassportNumber = model.Tickets[i].PassportNumber,
                    TicketPrice = model.Tickets[i].TicketPrice,
                };
                allTickets.Add(ticket);

            }

            await _unitOfWork.Repository<Ticket>().AddRangeAsync(allTickets);
            await _unitOfWork.CompleteAsync();

            foreach (var ticket in allTickets)
            {
                if (bookedFlightSeats.TryGetValue(ticket.FlightSeatId, out var flightSeat))
                {
                    flightSeat.TicketId = ticket.Id; 
                    _unitOfWork.Repository<FlightSeat>().Update(flightSeat);
                }

                var ticketIndex = allTickets.IndexOf(ticket);
                if (model.Tickets![ticketIndex].SelectedAddOnIds.Any())
                {
                    var addOns = model.Tickets![ticketIndex].SelectedAddOnIds.Select(addOnId => new TicketAddOns
                    {
                        TicketId = ticket.Id,
                        AddOnID = addOnId
                    }).ToList();

                    await _unitOfWork.Repository<TicketAddOns>().AddRangeAsync(addOns);
                }
            }

            await _unitOfWork.CompleteAsync();
            await transaction.CommitAsync();

            // payment

            long amountInCents = (long)(totalPrice * 100);

            var payment = new Payment
            {
                BookingID = book.Id,
                Amount = amountInCents,
                PaymentDate = DateTime.UtcNow,
                PaymentStatus = PaymentStatus.Pending,
            };
            await _unitOfWork.Repository<Payment>().AddAsync(payment);
            await _unitOfWork.CompleteAsync();

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
                SuccessUrl = Url.Action("Success", "Book", new { id = payment.Id }, Request.Scheme),
                CancelUrl = Url.Action("Cancel", "Book", new { id = payment.Id }, Request.Scheme)
            };

            var service = new SessionService();
            Session session = service.Create(options);

            payment.SessionId = session.Id;
            _unitOfWork.Repository<Payment>().Update(payment);
            await _unitOfWork.CompleteAsync();

            return Redirect(session.Url);
        }

        #endregion

        #region Success

        public async Task<IActionResult> Success(int id)
        {
            var payment = await _unitOfWork.Repository<Payment>().GetByIdAsync(id);
            var sessionService = new SessionService();
            var session = await sessionService.GetAsync(payment!.SessionId);  

            if (session.PaymentStatus == "paid") 
            {
                var book = await _unitOfWork.Repository<Booking>().GetByIdAsync(payment.BookingID);
                book!.Status = Status.Confirmed;
                payment.PaymentStatus = PaymentStatus.Approved;
                payment.PaymentIntentId = session.PaymentIntentId;
                await _unitOfWork.CompleteAsync();

                await SendBookingPDFEmail(payment.BookingID);
            }
            return View(payment.BookingID);
        }

        #endregion

        #region Cancel

        public async Task<IActionResult> Cancel(int id)
        {
            var spec = new BaseSpecification<Payment>(p => p.Id == id);
            spec.ComplexIncludes.Add(c => c.Include(t => t.Booking)
                    .ThenInclude(a => a.Tickets)
                    .ThenInclude(d => d.FlightSeat));

            spec.ComplexIncludes.Add(c => c.Include(t => t.Booking)
                    .ThenInclude(a => a.Tickets)
                    .ThenInclude(d => d.TicketAddOns));  

            var payment = await _unitOfWork.Repository<Payment>().GetEntityWithSpecAsync(spec);

            foreach (var ticket in payment!.Booking.Tickets)
            {
                if (ticket.TicketAddOns != null && ticket.TicketAddOns.Any())
                {
                    _unitOfWork.Repository<TicketAddOns>().RemoveRange(ticket.TicketAddOns);
                }
                if (ticket.FlightSeat != null)
                {
                    if (ticket.FlightSeat != null)
                    {
                        ticket.FlightSeat.IsAvailable = true;
                        ticket.FlightSeat.TicketId = null;
                        _unitOfWork.Repository<FlightSeat>().Update(ticket.FlightSeat);
                    }
                }
            }
            // حذف Payment (Cascade delete سيحذف Booking + Tickets تلقائياً)
            _unitOfWork.Repository<Payment>().Delete(payment);

            await _unitOfWork.CompleteAsync();
            return View();
        }

        #endregion

        #region method

        private async Task SendBookingPDFEmail(int bookingId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
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

            if (booking?.AppUser?.Email == null) return;

            var pdfResult = new ViewAsPdf("/Areas/Customer/Views/MyBookings/BookingPDF.cshtml", booking, ViewData)
            {
                PageMargins = new Rotativa.AspNetCore.Options.Margins { Top = 20, Right = 20, Bottom = 20, Left = 20 },
                PageOrientation = Rotativa.AspNetCore.Options.Orientation.Portrait,
                PageSize = Rotativa.AspNetCore.Options.Size.A4
            };

            var actionContext = new ActionContext(HttpContext, RouteData, ControllerContext.ActionDescriptor);
            byte[] pdfBytes = await pdfResult.BuildFile(actionContext);

            var emailSender = HttpContext.RequestServices.GetRequiredService<IEmailSender>();
            await emailSender.SendEmailAsyncWithAttachment(
                booking.AppUser.Email,
                $"Your Booking Confirmation - {booking.PNR}",
                "Dear Customer,<br/><br/>Your booking has been confirmed successfully!<br/><br/>Please find your e-ticket attached.",
                pdfBytes,
                $"Booking_{booking.PNR}.pdf"
            );
        }

        #endregion
    }
}
