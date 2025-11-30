using DAL.models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DAL.Configrations
{
    internal class AirlineConfigration : IEntityTypeConfiguration<Airline>
    {
        public void Configure(EntityTypeBuilder<Airline> builder)
        {
            builder.HasKey(e => e.Id);

            builder.Property(e => e.Name)
             .HasMaxLength(100)
             .IsRequired();

            builder.Property(e => e.Code)
                .HasMaxLength(50);
        }
    }
}
