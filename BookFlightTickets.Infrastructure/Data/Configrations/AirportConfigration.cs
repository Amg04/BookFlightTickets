using BookFlightTickets.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookFlightTickets.Infrastructure.Data.Configrations
{
    internal class AirportConfigration : IEntityTypeConfiguration<Airport>
    {
        public void Configure(EntityTypeBuilder<Airport> builder)
        {
            builder.HasKey(e => e.Id);

            builder.Property(e => e.Name)
             .HasMaxLength(100)
             .IsRequired();

            builder.Property(e => e.Code)
            .HasMaxLength(50)
            .IsRequired();

            builder.Property(e => e.City)
           .HasMaxLength(100);

            builder.Property(e => e.Country)
          .HasMaxLength(100);
        }
    }
}
