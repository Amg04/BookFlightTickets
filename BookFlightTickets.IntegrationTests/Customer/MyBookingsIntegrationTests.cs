using BookFlightTickets.Core.Domain.Entities;
using BookFlightTickets.UI;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;

namespace BookFlightTickets.IntegrationTests.Customer
{
    public class MyBookingsIntegrationTests : BaseIntegrationTest
    {
        public MyBookingsIntegrationTests(WebApplicationFactory<Program> factory) : base(factory)
        {
            InitializeFactory(services => { });
        }

        #region Index

        [Fact]
        public async Task Index_AuthorizedUserWithBookings_ReturnsViewWithBookings()
        {
            // Arrange
            var userId = "test-user-id";
            await ExecuteWithDbContextAsync(async dbContext =>
            {
                var airline = new Airline { Name = "Test Airline", Code = "TA" };
                var airplane = new Airplane { Model = "Boeing 737" };
                var airport1 = new Airport { Name = "JFK", Code = "JFK" };
                var airport2 = new Airport { Name = "LAX", Code = "LAX" };
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

                var booking = new Booking
                {
                    UserId = userId,
                    FlightId = flight.Id,
                    BookingDate = DateTime.UtcNow,
                    PNR = "ABC123",
                    TotalPrice = 200,
                    Status = Status.Confirmed
                };
                dbContext.Bookings.Add(booking);
                await dbContext.SaveChangesAsync();

                var ticket = new Ticket
                {
                    BookingID = booking.Id,
                    TicketNumber = "T123",
                    FirstName = "John",
                    LastName = "Doe",
                    PassportNumber = "AB123",
                    TicketPrice = 200
                };
                dbContext.Tickets.Add(ticket);
                await dbContext.SaveChangesAsync();
            });

            var client = CreateAuthenticatedClient();

            // Act
            var response = await client.GetAsync("/Customer/MyBookings/Index");
            var content = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("ABC123", content);
            Assert.Contains("Test Airline", content);
            Assert.Contains("JFK", content);
            Assert.Contains("LAX", content);
        }

        [Fact]
        public async Task Index_AuthorizedUserWithNoBookings_ReturnsViewWithEmptyList()
        {
            var client = CreateAuthenticatedClient();

            // Act
            var response = await client.GetAsync("/Customer/MyBookings/Index");
            var content = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.DoesNotContain("ABC123", content);
        }

        [Fact]
        public async Task Index_UnauthorizedUser_RedirectsToLogin()
        {
            var client = CreateUnauthenticatedClient();

            // Act
            var response = await client.GetAsync("/Customer/MyBookings/Index");

            // Assert
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            var location = response.Headers.Location;
            Assert.NotNull(location);
            Assert.Equal("/Identity/Account/Login", location.LocalPath);
        }

        #endregion

        #region BookingPDF

        [Fact]
        public async Task BookingPDF_AuthorizedUserWithInvalidBooking_ReturnsNotFound()
        {
            var client = CreateAuthenticatedClient();

            // Act
            var response = await client.GetAsync("/Customer/MyBookings/BookingPDF?bookingId=999");

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task BookingPDF_UnauthorizedUser_RedirectsToLogin()
        {
            var client = CreateUnauthenticatedClient();

            // Act
            var response = await client.GetAsync("/Customer/MyBookings/BookingPDF?bookingId=1");

            // Assert
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            var location = response.Headers.Location;
            Assert.NotNull(location);
            Assert.Equal("/Identity/Account/Login", location.LocalPath);
        }

        #endregion
    }
}