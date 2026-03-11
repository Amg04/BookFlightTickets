using BookFlightTickets.Core.Domain.Entities;
using BookFlightTickets.UI;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;

namespace BookFlightTickets.IntegrationTests.Admin
{
    public class TicketAddOnsIntegrationTests : BaseIntegrationTest
    {
        public TicketAddOnsIntegrationTests(WebApplicationFactory<Program> factory) : base(factory)
        {
            InitializeFactory(services => { });
        }

        #region Index (GET)

        [Fact]
        public async Task Index_WhenTicketAddOnsExist_ReturnsViewWithTicketAddOns()
        {
            await ExecuteWithDbContextAsync(async dbContext =>
            {
                // 1. Create a flight (needed for ticket)
                var airline = new Airline { Name = "Test Airline", Code = "TAD" };
                var airplane = new Airplane { Model = "Boeing 737", SeatCapacity = 180, Airline = airline };
                var airport1 = new Airport { Name = "JFK", Code = "JFK", City = "New York", Country = "USA" };
                var airport2 = new Airport { Name = "LAX", Code = "LAX", City = "Los Angeles", Country = "USA" };
                dbContext.Airlines.Add(airline);
                dbContext.Airplanes.Add(airplane);
                dbContext.Airports.AddRange(airport1, airport2);
                await dbContext.SaveChangesAsync();

                var flight = new Flight
                {
                    AirlineId = airline.Id,
                    AirplaneId = airplane.Id,
                    DepartureAirportID = airport1.Id,
                    ArrivalAirportID = airport2.Id,
                    DepartureTime = DateTime.UtcNow,
                    ArrivalTime = DateTime.UtcNow.AddHours(5),
                    BasePrice = 200
                };
                dbContext.Flights.Add(flight);
                await dbContext.SaveChangesAsync();

                // 2. Create a user and booking
                var user = new AppUser { Id = "test-user-id", UserName = "test@example.com", Email = "test@example.com", FirstName = "Ahmed" };
                dbContext.Users.Add(user);
                await dbContext.SaveChangesAsync();

                var booking = new Booking
                {
                    UserId = user.Id,
                    FlightId = flight.Id,
                    BookingDate = DateTime.UtcNow,
                    PNR = "ABC123",
                    TotalPrice = 200,
                    Status = Status.Confirmed,
                    Payment = new Payment { Amount = 200, PaymentDate = DateTime.UtcNow, PaymentStatus = PaymentStatus.Approved }
                };
                dbContext.Bookings.Add(booking);
                await dbContext.SaveChangesAsync();

                // 3. Create a ticket
                var seat = new Seat { AirplaneId = airplane.Id, Row = "A", Number = 1, Class = SeatClass.Economy, Price = 0 };
                dbContext.Seats.Add(seat);
                await dbContext.SaveChangesAsync();

                var ticket = new Ticket
                {
                    BookingID = booking.Id,
                    TicketNumber = "T123",
                    FlightSeatId = seat.Id,
                    FirstName = "John",
                    LastName = "Doe",
                    PassportNumber = "AB123456",
                    TicketPrice = 200
                };
                dbContext.Tickets.Add(ticket);
                await dbContext.SaveChangesAsync();

                // 4. Create an addon
                var addon = new AddOn { Name = "Extra Baggage", Price = 50 };
                dbContext.AddOns.Add(addon);
                await dbContext.SaveChangesAsync();

                // 5. Create TicketAddOns (relationship)
                var ticketAddOn = new TicketAddOns
                {
                    TicketId = ticket.Id,
                    AddOnID = addon.Id
                };
                dbContext.TicketAddOns.Add(ticketAddOn);
                await dbContext.SaveChangesAsync();
            });

            var client = CreateAdminClient();

            // Act
            var response = await client.GetAsync("/Admin/TicketAddOns/Index");
            var content = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("TicketAddOns", content); 
        }

        [Fact]
        public async Task Index_WhenNoTicketAddOns_ReturnsViewWithInfoMessage()
        {
            // Ensure no data
            await ExecuteWithDbContextAsync(async dbContext =>
            {
                dbContext.TicketAddOns.RemoveRange(dbContext.TicketAddOns);
                await dbContext.SaveChangesAsync();
            });

            var client = CreateAdminClient();

            // Act
            var response = await client.GetAsync("/Admin/TicketAddOns/Index");
            var content = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("No ticket AddOns available", content);
        }

        [Fact]
        public async Task Index_UnauthorizedUser_ReturnsRedirectToLogin()
        {
            var client = CreateUnauthenticatedClient();
            var response = await client.GetAsync("/Admin/TicketAddOns/Index");

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            var location = response.Headers.Location;
            Assert.NotNull(location);
            Assert.Equal("/Identity/Account/Login", location.LocalPath);
        }

        [Fact]
        public async Task Index_UserWithoutAdminRole_ReturnsForbidden()
        {
            var client = CreateAuthenticatedClient(); // regular authenticated user, not admin
            var response = await client.GetAsync("/Admin/TicketAddOns/Index");

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        #endregion
    }
}