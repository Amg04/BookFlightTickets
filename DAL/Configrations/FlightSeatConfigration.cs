using DAL.models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DAL.Configrations
{
    internal class FlightSeatConfigration : IEntityTypeConfiguration<FlightSeat>
    {
        public void Configure(EntityTypeBuilder<FlightSeat> builder)
        {
            builder.HasKey(e => e.Id);

            builder.HasOne(e => e.Flight)
            .WithMany(s => s.FlightSeats)
            .HasForeignKey(e => e.FlightId)
            .OnDelete(DeleteBehavior.Cascade);


            builder.HasOne(e => e.Seat)
            .WithMany(s => s.FlightSeats)
            .HasForeignKey(e => e.SeatId)
            .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(fs => fs.Ticket)
            .WithOne(t => t.FlightSeat)
            .HasForeignKey<Ticket>(t => t.FlightSeatId)
            .OnDelete(DeleteBehavior.Restrict);

            builder.Property(e => e.IsAvailable)
            .IsRequired();

            builder.HasIndex(e => new { e.FlightId, e.SeatId })
           .IsUnique();
        }
    }
}
