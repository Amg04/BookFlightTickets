using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using DAL.models;

namespace DAL.Configrations
{
    internal class TicketConfigration : IEntityTypeConfiguration<Ticket>
    {
        public void Configure(EntityTypeBuilder<Ticket> builder)
        {
            builder.HasKey(e => e.Id);

            builder.HasOne(e => e.Booking)
             .WithMany(s => s.Tickets)
             .HasForeignKey(e => e.BookingID)
             .OnDelete(DeleteBehavior.Cascade);

            builder.Property(e => e.TicketNumber)
            .IsRequired()
            .HasMaxLength(50);

            builder.HasIndex(e => e.TicketNumber)
                .IsUnique();

            builder.HasOne(e => e.FlightSeat)
                .WithOne(s => s.Ticket)
                .HasForeignKey<Ticket>(e => e.FlightSeatId) 
                .OnDelete(DeleteBehavior.Cascade);  
        }
    }
}
