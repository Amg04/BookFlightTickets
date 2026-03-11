using BookFlightTickets.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookFlightTickets.Infrastructure.Data.Configrations
{
    internal class TicketAddOnConfigration : IEntityTypeConfiguration<TicketAddOns>
    {
        public void Configure(EntityTypeBuilder<TicketAddOns> builder)
        {
            builder.HasKey(e => e.Id);

            builder.HasOne(e => e.Ticket)
            .WithMany(s => s.TicketAddOns)
            .HasForeignKey(e => e.TicketId)
            .OnDelete(DeleteBehavior.Cascade);


            builder.HasOne(e => e.AddOn)
            .WithMany(s => s.TicketAddOns)
            .HasForeignKey(e => e.AddOnID)
            .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(e => new { e.TicketId, e.AddOnID })
           .IsUnique();
        }
    }
}
