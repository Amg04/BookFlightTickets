using BookFlightTickets.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookFlightTickets.Infrastructure.Data.Configrations
{
    internal class AppUserConfigration : IEntityTypeConfiguration<AppUser>
    {
        public void Configure(EntityTypeBuilder<AppUser> builder)
        {
            builder.HasKey(e => e.Id);

            builder.Property(e => e.FirstName)
             .HasMaxLength(100)
             .IsRequired();

              builder.Property(e => e.Role)
             .HasMaxLength(500)
             .IsRequired();

            builder.Property(e => e.LastName)
                .HasMaxLength(100);

            builder.Property(e => e.PassportNumber)
             .HasMaxLength(100);
        }
    }
}
