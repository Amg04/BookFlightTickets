using BookFlightTickets.Core.Domain.Entities;
using BookFlightTickets.UI;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;

namespace BookFlightTickets.IntegrationTests.Admin
{
    public class BookingsIntegrationTests : BaseIntegrationTest
    {
        public BookingsIntegrationTests(WebApplicationFactory<Program> factory) : base(factory)
        {
            InitializeFactory(services => { });
        }

        #region Index (GET)

        [Fact]
        public async Task Index_WhenBookingsExist_ReturnsViewWithBookings()
        {
            // Arrange: create necessary data (airline, airports, flight, booking, payment)
            await ExecuteWithDbContextAsync(async dbContext =>
            {
                var airline = new Airline { Name = "Test Airline", Code = "TA" };
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
            });

            var client = CreateAdminClient();

            // Act
            var response = await client.GetAsync("/Admin/Bookings/Index");
            var content = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("ABC123", content);
            Assert.Contains("Bookings", content);
        }

        [Fact]
        public async Task Index_WhenNoBookings_ReturnsViewWithInfoMessage()
        {
            // Ensure no data
            await ExecuteWithDbContextAsync(async dbContext =>
            {
                dbContext.Bookings.RemoveRange(dbContext.Bookings);
                await dbContext.SaveChangesAsync();
            });

            var client = CreateAdminClient();

            // Act
            var response = await client.GetAsync("/Admin/Bookings/Index");
            var content = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("No bookings available", content);
        }


        [Fact]
        public async Task Index_UnauthorizedUser_ReturnsRedirectToLogin()
        {
            var client = CreateUnauthenticatedClient();
            var response = await client.GetAsync("/Admin/Bookings/Index");

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            var location = response.Headers.Location;
            Assert.NotNull(location);
            Assert.Equal("/Identity/Account/Login", location.LocalPath);
        }

        [Fact]
        public async Task Index_UserWithoutAdminRole_ReturnsForbidden()
        {
            var client = CreateAuthenticatedClient();
            var response = await client.GetAsync("/Admin/Bookings/Index");

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        #endregion
    }
}