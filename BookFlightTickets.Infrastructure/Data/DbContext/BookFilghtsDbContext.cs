using BookFlightTickets.Core.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace BookFlightTickets.Infrastructure.Data.DbContext
{
    public class BookFilghtsDbContext : IdentityDbContext<AppUser>
    {
        public BookFilghtsDbContext(DbContextOptions<BookFilghtsDbContext> options) : base(options) { }
        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            configurationBuilder.Properties<decimal>().HavePrecision(9, 2);
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);
        }
        public DbSet<Airport> Airports { get; set; }
        public DbSet<Airline> Airlines { get; set; }
        public DbSet<Airplane> Airplanes { get; set; }
        public DbSet<Flight> Flights { get; set; }
        public DbSet<Booking> Bookings { get; set; }
        public DbSet<Ticket> Tickets { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<AddOn> AddOns { get; set; }
        public DbSet<TicketAddOns> TicketAddOns { get; set; }
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

            modelBuilder.Entity<Airline>().HasData( new Airline { Id = 1, Name = "EgyptAir", Code = "EGY" });

            #endregion

            #region Airport

            modelBuilder.Entity<Airport>().HasData( 
                new Airport { Id = 1, Name = "Cairo International Airport", Code = "CAI" }, 
                new Airport { Id = 2, Name = "Dubai International Airport", Code = "DXB" });

            #endregion

            #region Airplane

            modelBuilder.Entity<Airplane>().HasData( new Airplane { Id = 1, Model = "Boeing 999", SeatCapacity = 8, AirlineId = 1 });

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
                   AirlineId = 1,
                   AirplaneId = 1,
                   DepartureAirportID = 2,
                   ArrivalAirportID = 1,
                   DepartureTime = new DateTime(2025, 1, 12, 08, 00, 0),
                   ArrivalTime = new DateTime(2025, 1, 12, 12, 00, 0),
                   BasePrice = 3200,
                   Status = FlightStatus.Scheduled
               });

            #endregion

            #region AddOn

            modelBuilder.Entity<AddOn>().HasData(
                    new AddOn { Id = 1, Name = "Extra Baggage", Price = 150 },
                    new AddOn { Id = 4, Name = "Priority Boarding", Price = 50 },
                    new AddOn { Id = 3, Name = "Premium Meal", Price = 100 });

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
               new FlightSeat { Id = 15, FlightId = 2, SeatId = 7, IsAvailable = true },
               new FlightSeat { Id = 16, FlightId = 2, SeatId = 8, IsAvailable = true });

            #endregion

            #endregion
        }
    }
}
