using BAL.model;
using DAL.models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace DAL.Data
{
    public class BookFilghtsDbContext : IdentityDbContext<AppUser>
    {
        public BookFilghtsDbContext(DbContextOptions<BookFilghtsDbContext> options) : base(options) { }
        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            configurationBuilder.Properties<decimal>().HavePrecision(9, 2);
        }

        public DbSet<Airport> Airports { get; set; }
        public DbSet<Airline> Airlines { get; set; }
        public DbSet<Airplane> Airplanes { get; set; }
        public DbSet<Flight> Flights { get; set; }
        public DbSet<Booking> Bookings { get; set; }
        public DbSet<Ticket> Tickets { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<AddOn> AddOns { get; set; }
        public DbSet<TicketAddOns> BookingAddOns { get; set; }
        public DbSet<Seat> Seats { get; set; }
        public DbSet<FlightSeat> FlightSeats { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            #region rename identity Table (AspNetUsers)

            modelBuilder.Entity<AppUser>().ToTable("Users", "Security");
            modelBuilder.Entity<IdentityRole>().ToTable("Roles", "Security");
            modelBuilder.Entity<IdentityUserRole<string>>().ToTable("UserRoles", "Security");
            modelBuilder.Entity<IdentityUserClaim<string>>().ToTable("UserClaims", "Security");
            modelBuilder.Entity<IdentityUserLogin<string>>().ToTable("UserLogins", "Security");
            modelBuilder.Entity<IdentityRoleClaim<string>>().ToTable("RoleClaims", "Security");
            modelBuilder.Entity<IdentityUserToken<string>>().ToTable("UserTokens", "Security");
            #endregion

            modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

            #region Seed Data

            #region Airline

            modelBuilder.Entity<Airline>().HasData(
                new Airline { Id = 1, Name = "EgyptAir", Code = "EGY" },
                new Airline { Id = 2, Name = "Emirates", Code = "EMR" },
                new Airline { Id = 3, Name = "Qatar Airways", Code = "QTR" },
                new Airline { Id = 4, Name = "KSA", Code = "KSA" });

            #endregion

            #region  Airport
            
            modelBuilder.Entity<Airport>().HasData(
                new Airport { Id = 1, Name = "Cairo International Airport", Code = "CAI" },
                new Airport { Id = 2, Name = "Dubai International Airport", Code = "DXB" },
                new Airport { Id = 3, Name = "Doha International Airport", Code = "DOH" },
                new Airport { Id = 4, Name = "Istanbul Airport", Code = "IST" });
            #endregion

            #region Airplane

            modelBuilder.Entity<Airplane>().HasData(
               new Airplane { Id = 1, Model = "Boeing 999", SeatCapacity = 8, AirlineId = 1 },
               new Airplane { Id = 2, Model = "Airbus A320", SeatCapacity = 7, AirlineId = 1 },
               new Airplane { Id = 3, Model = "Boeing 555", SeatCapacity = 5, AirlineId = 2 },
               new Airplane { Id = 4, Model = "Boeing 777", SeatCapacity = 4, AirlineId = 2 });

            #endregion

            #region  Flight
            modelBuilder.Entity<Flight>().HasData(
               new Flight
               {
                   Id = 1,
                   AirlineId = 1,
                   AirplaneId = 1,
                   DepartureAirportID = 1,
                   ArrivalAirportID = 2,
                   DepartureTime = new DateTime(2025, 1, 01, 10, 30, 0),
                   ArrivalTime = new DateTime(2025, 1, 01, 15, 00, 0),
                   BasePrice = 2500,
                   Status = FlightStatus.Scheduled
               },
               new Flight
               {
                   Id = 2,
                   AirlineId = 2,
                   AirplaneId = 2,
                   DepartureAirportID = 2,
                   ArrivalAirportID = 3,
                   DepartureTime = new DateTime(2025, 1, 12, 08, 00, 0),
                   ArrivalTime = new DateTime(2025, 1, 12, 12, 00, 0),
                   BasePrice = 3200,
                   Status = FlightStatus.Scheduled
               },
               new Flight
               {
                   Id = 3,
                   AirlineId = 4,
                   AirplaneId = 3,
                   DepartureAirportID = 3,
                   ArrivalAirportID = 1,
                   DepartureTime = new DateTime(2025, 1, 12, 12, 00, 0),
                   ArrivalTime = new DateTime(2025, 1, 12, 6, 00, 0),
                   BasePrice = 3200,
                   Status = FlightStatus.Scheduled
               },
               new Flight
               {
                   Id = 4,
                   AirlineId = 3,
                   AirplaneId = 4,
                   DepartureAirportID = 4,
                   ArrivalAirportID = 2,
                   DepartureTime = new DateTime(2025, 1, 03, 12, 00, 0),
                   ArrivalTime = new DateTime(2025, 1, 03, 6, 00, 0),
                   BasePrice = 3200,
                   Status = FlightStatus.Scheduled
               },
               new Flight
               {
                   Id = 5,
                   AirlineId = 2,
                   AirplaneId = 1,
                   DepartureAirportID = 1,
                   ArrivalAirportID = 3,
                   DepartureTime = new DateTime(2025, 1, 10, 12, 00, 0),
                   ArrivalTime = new DateTime(2025, 1, 10, 6, 00, 0),
                   BasePrice = 4900,
                   Status = FlightStatus.Scheduled
               },
               new Flight
               {
                   Id = 6,
                   AirlineId = 4,
                   AirplaneId = 2,
                   DepartureAirportID = 2,
                   ArrivalAirportID = 3,
                   DepartureTime = new DateTime(2025, 1, 04, 12, 00, 0),
                   ArrivalTime = new DateTime(2025, 1, 04, 6, 00, 0),
                   BasePrice = 8200,
                   Status = FlightStatus.Scheduled
               },
               new Flight
               {
                   Id = 7,
                   AirlineId = 2,
                   AirplaneId = 2,
                   DepartureAirportID = 3,
                   ArrivalAirportID = 1
                   ,
                   DepartureTime = new DateTime(2025, 2, 12, 12, 00, 0),
                   ArrivalTime = new DateTime(2025, 2, 12, 6, 00, 0),
                   BasePrice = 1200,
                   Status = FlightStatus.Scheduled
               },
               new Flight
               {
                   Id = 8,
                   AirlineId = 1,
                   AirplaneId = 2,
                   DepartureAirportID = 3,
                   ArrivalAirportID = 4,
                   DepartureTime = new DateTime(2025, 4, 11, 12, 00, 0),
                   ArrivalTime = new DateTime(2025, 4, 11, 6, 00, 0),
                   BasePrice = 6200,
                   Status = FlightStatus.Scheduled
               },
               new Flight
               {
                   Id = 9,
                   AirlineId = 2,
                   AirplaneId = 1,
                   DepartureAirportID = 2,
                   ArrivalAirportID = 1,
                   DepartureTime = new DateTime(2025, 5, 22, 12, 00, 0),
                   ArrivalTime = new DateTime(2025, 5, 22, 6, 00, 0),
                   BasePrice = 5300,
                   Status = FlightStatus.Scheduled
               },
               new Flight
               {
                   Id = 10,
                   AirlineId = 3,
                   AirplaneId = 4,
                   DepartureAirportID = 4,
                   ArrivalAirportID = 2,
                   DepartureTime = new DateTime(2025, 7, 23, 12, 00, 0),
                   ArrivalTime = new DateTime(2025, 7, 23, 6, 00, 0),
                   BasePrice = 3500,
                   Status = FlightStatus.Scheduled
               },
               new Flight
               {
                   Id = 11,
                   AirlineId = 3,
                   AirplaneId = 2,
                   DepartureAirportID = 3,
                   ArrivalAirportID = 2,
                   DepartureTime = new DateTime(2025, 1, 23, 12, 00, 0),
                   ArrivalTime = new DateTime(2025, 1, 23, 6, 00, 0),
                   BasePrice = 7600,
                   Status = FlightStatus.Scheduled
               },
               new Flight
               {
                   Id = 12,
                   AirlineId = 4,
                   AirplaneId = 1,
                   DepartureAirportID = 2,
                   ArrivalAirportID = 1,
                   DepartureTime = new DateTime(2025, 9, 24, 12, 00, 0),
                   ArrivalTime = new DateTime(2025, 9, 24, 6, 00, 0),
                   BasePrice = 1500,
                   Status = FlightStatus.Scheduled
               });

            #endregion

            #region AddOn
            modelBuilder.Entity<AddOn>().HasData(
                    new AddOn { Id = 1, Name = "Extra Baggage", price = 150 },
                    new AddOn { Id = 4, Name = "Priority Boarding", price = 50 },
                    new AddOn { Id = 3, Name = "Premium Meal", price = 100 });
            #endregion

            #region Seat
            modelBuilder.Entity<Seat>().HasData(
                    new Seat { Id = 1, AirplaneId = 1, Row = "A", Number = 1, Class = SeatClass.Economy, Price = 100 },
                    new Seat { Id = 2, AirplaneId = 1, Row = "A", Number = 2, Class = SeatClass.Economy, Price = 100 },
                    new Seat { Id = 3, AirplaneId = 1, Row = "B", Number = 1, Class = SeatClass.Business, Price = 200 },
                    new Seat { Id = 4, AirplaneId = 1, Row = "B", Number = 2, Class = SeatClass.Business, Price = 200 },
                    new Seat { Id = 5, AirplaneId = 1, Row = "C", Number = 1, Class = SeatClass.First, Price = 300 },
                    new Seat { Id = 6, AirplaneId = 1, Row = "C", Number = 2, Class = SeatClass.First, Price = 300 },
                    new Seat { Id = 7, AirplaneId = 1, Row = "D", Number = 1, Class = SeatClass.Economy, Price = 100 },
                    new Seat { Id = 8, AirplaneId = 1, Row = "D", Number = 2, Class = SeatClass.Economy, Price = 100 }
                    );
            #endregion

            #region FlightSeat

            modelBuilder.Entity<FlightSeat>().HasData(
               new FlightSeat { Id = 1, FlightId = 1, SeatId = 1, IsAvailable = true },
               new FlightSeat { Id = 2, FlightId = 1, SeatId = 2, IsAvailable = true },
               new FlightSeat { Id = 3, FlightId = 1, SeatId = 3, IsAvailable = true },
               new FlightSeat { Id = 4, FlightId = 1, SeatId = 4, IsAvailable = true },
               new FlightSeat { Id = 5, FlightId = 1, SeatId = 5, IsAvailable = true },
               new FlightSeat { Id = 6, FlightId = 1, SeatId = 6, IsAvailable = true },
               new FlightSeat { Id = 7, FlightId = 1, SeatId = 7, IsAvailable = true },
               new FlightSeat { Id = 8, FlightId = 1, SeatId = 8, IsAvailable = true });

            modelBuilder.Entity<FlightSeat>().HasData(
               new FlightSeat { Id = 9, FlightId = 2, SeatId = 1, IsAvailable = true },
               new FlightSeat { Id = 10, FlightId = 2, SeatId = 2, IsAvailable = true },
               new FlightSeat { Id = 11, FlightId = 2, SeatId = 3, IsAvailable = true },
               new FlightSeat { Id = 12, FlightId = 2, SeatId = 4, IsAvailable = true },
               new FlightSeat { Id = 13, FlightId = 2, SeatId = 5, IsAvailable = true },
               new FlightSeat { Id = 14, FlightId = 2, SeatId = 6, IsAvailable = true },
               new FlightSeat { Id = 15, FlightId = 2, SeatId = 7, IsAvailable = true });

            modelBuilder.Entity<FlightSeat>().HasData(
              new FlightSeat { Id = 16, FlightId = 3, SeatId = 1, IsAvailable = true },
              new FlightSeat { Id = 17, FlightId = 3, SeatId = 2, IsAvailable = true },
              new FlightSeat { Id = 18, FlightId = 3, SeatId = 3, IsAvailable = true },
              new FlightSeat { Id = 19, FlightId = 3, SeatId = 4, IsAvailable = true },
              new FlightSeat { Id = 20, FlightId = 3, SeatId = 5, IsAvailable = true });

            modelBuilder.Entity<FlightSeat>().HasData(
              new FlightSeat { Id = 23, FlightId = 4, SeatId = 1, IsAvailable = true },
              new FlightSeat { Id = 24, FlightId = 4, SeatId = 2, IsAvailable = true },
              new FlightSeat { Id = 25, FlightId = 4, SeatId = 3, IsAvailable = true },
              new FlightSeat { Id = 26, FlightId = 4, SeatId = 4, IsAvailable = true });


            modelBuilder.Entity<FlightSeat>().HasData(
                new FlightSeat { Id = 27, FlightId = 5, SeatId = 1, IsAvailable = true },
                new FlightSeat { Id = 28, FlightId = 5, SeatId = 2, IsAvailable = true },
                new FlightSeat { Id = 29, FlightId = 5, SeatId = 3, IsAvailable = true },
                new FlightSeat { Id = 30, FlightId = 5, SeatId = 4, IsAvailable = true },
                new FlightSeat { Id = 31, FlightId = 5, SeatId = 5, IsAvailable = true },
                new FlightSeat { Id = 32, FlightId = 5, SeatId = 6, IsAvailable = true },
                new FlightSeat { Id = 33, FlightId = 5, SeatId = 7, IsAvailable = true },
                new FlightSeat { Id = 34, FlightId = 5, SeatId = 8, IsAvailable = true });

            modelBuilder.Entity<FlightSeat>().HasData(
               new FlightSeat { Id = 35, FlightId = 6, SeatId = 1, IsAvailable = true },
               new FlightSeat { Id = 36, FlightId = 6, SeatId = 2, IsAvailable = true },
               new FlightSeat { Id = 37, FlightId = 6, SeatId = 3, IsAvailable = true },
               new FlightSeat { Id = 38, FlightId = 6, SeatId = 4, IsAvailable = true },
               new FlightSeat { Id = 39, FlightId = 6, SeatId = 5, IsAvailable = true },
               new FlightSeat { Id = 40, FlightId = 6, SeatId = 6, IsAvailable = true },
               new FlightSeat { Id = 41, FlightId = 6, SeatId = 7, IsAvailable = true });

            modelBuilder.Entity<FlightSeat>().HasData(
                new FlightSeat { Id = 82, FlightId = 7, SeatId = 1, IsAvailable = true },
                new FlightSeat { Id = 42, FlightId = 7, SeatId = 2, IsAvailable = true },
                new FlightSeat { Id = 43, FlightId = 7, SeatId = 3, IsAvailable = true },
                new FlightSeat { Id = 44, FlightId = 7, SeatId = 4, IsAvailable = true },
                new FlightSeat { Id = 45, FlightId = 7, SeatId = 5, IsAvailable = true },
                new FlightSeat { Id = 46, FlightId = 7, SeatId = 6, IsAvailable = true },
                new FlightSeat { Id = 47, FlightId = 7, SeatId = 7, IsAvailable = true });

            modelBuilder.Entity<FlightSeat>().HasData(
               new FlightSeat { Id = 48, FlightId = 8, SeatId = 1, IsAvailable = true },
               new FlightSeat { Id = 49, FlightId = 8, SeatId = 2, IsAvailable = true },
               new FlightSeat { Id = 50, FlightId = 8, SeatId = 3, IsAvailable = true },
               new FlightSeat { Id = 51, FlightId = 8, SeatId = 4, IsAvailable = true },
               new FlightSeat { Id = 52, FlightId = 8, SeatId = 5, IsAvailable = true },
               new FlightSeat { Id = 53, FlightId = 8, SeatId = 6, IsAvailable = true },
               new FlightSeat { Id = 54, FlightId = 8, SeatId = 7, IsAvailable = true });

            modelBuilder.Entity<FlightSeat>().HasData(
               new FlightSeat { Id = 55, FlightId = 9, SeatId = 1, IsAvailable = true },
               new FlightSeat { Id = 56, FlightId = 9, SeatId = 2, IsAvailable = true },
               new FlightSeat { Id = 57, FlightId = 9, SeatId = 3, IsAvailable = true },
               new FlightSeat { Id = 58, FlightId = 9, SeatId = 4, IsAvailable = true },
               new FlightSeat { Id = 59, FlightId = 9, SeatId = 5, IsAvailable = true },
               new FlightSeat { Id = 60, FlightId = 9, SeatId = 6, IsAvailable = true },
               new FlightSeat { Id = 61, FlightId = 9, SeatId = 8, IsAvailable = true },
               new FlightSeat { Id = 62, FlightId = 9, SeatId = 7, IsAvailable = true });

            modelBuilder.Entity<FlightSeat>().HasData(
                new FlightSeat { Id = 63, FlightId = 10, SeatId = 1, IsAvailable = true },
                new FlightSeat { Id = 64, FlightId = 10, SeatId = 2, IsAvailable = true },
                new FlightSeat { Id = 65, FlightId = 10, SeatId = 3, IsAvailable = true },
                new FlightSeat { Id = 66, FlightId = 10, SeatId = 4, IsAvailable = true });

            modelBuilder.Entity<FlightSeat>().HasData(
              new FlightSeat { Id = 67, FlightId = 11, SeatId = 1, IsAvailable = true },
              new FlightSeat { Id = 68, FlightId = 11, SeatId = 2, IsAvailable = true },
              new FlightSeat { Id = 69, FlightId = 11, SeatId = 3, IsAvailable = true },
              new FlightSeat { Id = 70, FlightId = 11, SeatId = 4, IsAvailable = true },
              new FlightSeat { Id = 71, FlightId = 11, SeatId = 5, IsAvailable = true },
              new FlightSeat { Id = 72, FlightId = 11, SeatId = 6, IsAvailable = true },
              new FlightSeat { Id = 73, FlightId = 11, SeatId = 7, IsAvailable = true });

            modelBuilder.Entity<FlightSeat>().HasData(
              new FlightSeat { Id = 74, FlightId = 12, SeatId = 1, IsAvailable = true },
              new FlightSeat { Id = 75, FlightId = 12, SeatId = 2, IsAvailable = true },
              new FlightSeat { Id = 76, FlightId = 12, SeatId = 3, IsAvailable = true },
              new FlightSeat { Id = 77, FlightId = 12, SeatId = 4, IsAvailable = true },
              new FlightSeat { Id = 78, FlightId = 12, SeatId = 5, IsAvailable = true },
              new FlightSeat { Id = 79, FlightId = 12, SeatId = 6, IsAvailable = true },
              new FlightSeat { Id = 80, FlightId = 12, SeatId = 8, IsAvailable = true },
              new FlightSeat { Id = 81, FlightId = 12, SeatId = 7, IsAvailable = true });

            #endregion

            #endregion
        }
    }
}
