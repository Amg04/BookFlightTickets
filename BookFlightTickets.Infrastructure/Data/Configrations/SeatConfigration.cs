using BookFlightTickets.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookFlightTickets.Infrastructure.Data.Configrations
{
    internal class SeatConfigration : IEntityTypeConfiguration<Seat>
    {
        public void Configure(EntityTypeBuilder<Seat> builder)
        {
            builder.HasKey(e => e.Id);

            builder.HasOne(e => e.Airplane)
               .WithMany(s => s.SeatTemplates)
               .HasForeignKey(e => e.AirplaneId)
               .OnDelete(DeleteBehavior.Cascade);

            builder.Property(e => e.Row)
                .HasMaxLength(100)
                .IsRequired();

            builder.Property(e => e.Number)
           .IsRequired()
           .HasColumnType("smallint");

            builder.Property(e => e.Class)
              .IsRequired();

            
        }
    }
}
