using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using BAL.model;

namespace DAL.Configrations
{
    internal class AppUserConfigration : IEntityTypeConfiguration<AppUser>
    {
        public void Configure(EntityTypeBuilder<AppUser> builder)
        {
            builder.HasKey(e => e.Id);

            builder.Property(e => e.FirstName)
             .HasMaxLength(100)
             .IsRequired();

            builder.Property(e => e.LastName)
                .HasMaxLength(100);

            builder.Property(e => e.PassportNumber)
             .HasMaxLength(100);
        }
    }
}
