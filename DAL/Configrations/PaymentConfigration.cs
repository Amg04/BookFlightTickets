using DAL.models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DAL.Configrations
{
    internal class PaymentConfigration : IEntityTypeConfiguration<Payment>
    {
        public void Configure(EntityTypeBuilder<Payment> builder)
        {
            builder.HasKey(e => e.Id);

            builder.Property(e => e.SessionId)
                .HasMaxLength(200);

             builder.Property(e => e.PaymentIntentId)
                .HasMaxLength(200);

             builder.Property(e => e.Amount)
                .IsRequired();

             builder.Property(e => e.PaymentDate)
                .IsRequired();

              builder.Property(e => e.PaymentStatus)
                .IsRequired();
        }
    }
}
