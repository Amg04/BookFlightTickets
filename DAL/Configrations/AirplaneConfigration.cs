using DAL.models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DAL.Configrations
{
    internal class AirplaneConfigration : IEntityTypeConfiguration<Airplane>
    {
        public void Configure(EntityTypeBuilder<Airplane> builder)
        {
            builder.HasKey(e => e.Id);

            builder.HasOne(e => e.Airline)
                .WithMany(s => s.Airplanes)
                .HasForeignKey(e => e.AirlineId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Property(e => e.Model)
                .HasMaxLength(100)
                .IsRequired();

            builder.Property(e => e.SeatCapacity)
               .IsRequired()
               .HasColumnType("smallint");
        }
    }
}
