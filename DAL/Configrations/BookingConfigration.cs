using DAL.models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DAL.Configrations
{
    internal class BookingConfigration : IEntityTypeConfiguration<Booking>
    {
        public void Configure(EntityTypeBuilder<Booking> builder)
        {
            builder.HasKey(e => e.Id);

            builder.Property(e => e.UserId)
                .IsRequired();

            builder.HasOne(e => e.AppUser)
               .WithMany(s => s.Bookings)
               .HasForeignKey(e => e.UserId)
               .OnDelete(DeleteBehavior.NoAction);

            builder.HasOne(e => e.Flight)
            .WithMany(s => s.Bookings)
            .HasForeignKey(e => e.FlightId)
            .OnDelete(DeleteBehavior.NoAction);


            builder.Property(e => e.BookingDate)
                .IsRequired();


            builder.Property(e => e.PNR)
                .IsRequired()
                .HasMaxLength(50);

            builder.HasIndex(e => e.PNR)
                .IsUnique();


            builder.Property(e => e.TotalPrice)
                .IsRequired();

            builder.Property(e => e.Status)
                .IsRequired();

            builder.Property(e => e.LastUpdated)
                .IsRequired();

            builder.HasOne(e => e.Payment)
                .WithOne(p => p.Booking)
                .HasForeignKey<Payment>(p => p.BookingID)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
