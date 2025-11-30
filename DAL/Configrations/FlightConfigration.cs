using DAL.models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DAL.Configrations
{
    internal class FlightConfigration : IEntityTypeConfiguration<Flight>
    {
        public void Configure(EntityTypeBuilder<Flight> builder)
        {
            builder.HasKey(e => e.Id);

            builder.HasOne(e => e.Airline)
                .WithMany(s => s.Flights)
                .HasForeignKey(e => e.AirlineId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.HasOne(e => e.Airplane)
              .WithMany(s => s.Flights)
              .HasForeignKey(e => e.AirplaneId)
              .OnDelete(DeleteBehavior.NoAction);


            builder.HasOne(e => e.DepartureAirport)
                .WithMany(a => a.DepartureFlights)
                .HasForeignKey(e => e.DepartureAirportID)
                .OnDelete(DeleteBehavior.NoAction);

            builder.HasOne(e => e.ArrivalAirport)
                .WithMany(a => a.ArrivalFlights)
                .HasForeignKey(e => e.ArrivalAirportID)
                .OnDelete(DeleteBehavior.NoAction);


            builder.Property(e => e.DepartureTime)
                .IsRequired();


            builder.Property(e => e.ArrivalTime)
                .IsRequired();

            builder.Property(e => e.BasePrice)
                .IsRequired();

            builder.Property(e => e.Status)
                .IsRequired();

        }
    }
}
