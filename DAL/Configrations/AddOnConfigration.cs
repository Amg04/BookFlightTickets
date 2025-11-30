using DAL.models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DAL.Configrations
{
    internal class AddOnConfigration : IEntityTypeConfiguration<AddOn>
    {
        public void Configure(EntityTypeBuilder<AddOn> builder)
        {
            builder.HasKey(e => e.Id);

            builder.Property(e => e.Name)
             .HasMaxLength(100)
             .IsRequired();


            builder.Property(e => e.price)
                .IsRequired()
                .HasColumnType("smallint");

        }
    }
}
