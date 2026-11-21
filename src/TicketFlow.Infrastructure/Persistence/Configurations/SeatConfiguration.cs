using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TicketFlow.Domain.Entities;

namespace TicketFlow.Infrastructure.Persistence.Configurations;

public class SeatConfiguration : IEntityTypeConfiguration<Seat>
{
    public void Configure(EntityTypeBuilder<Seat> e)
    {
        e.ToTable("Seats", t => t.HasCheckConstraint("CK_Seats_Price_NonNegative", "[Price] >= 0"));
        e.HasKey(s => s.Id);

        e.Property(s => s.SeatCode).HasMaxLength(20).IsRequired();
        e.Property(s => s.Section).HasMaxLength(50).IsRequired();
        e.Property(s => s.Price).HasPrecision(18, 2);
        e.Property(s => s.Status)
            .HasConversion<string>()
            .HasMaxLength(20);
        e.Property(s => s.RowVersion).IsRowVersion();

        e.HasIndex(s => new { s.EventId, s.SeatCode })
            .IsUnique()
            .HasDatabaseName("UX_Seats_EventId_SeatCode");

        e.HasIndex(s => new { s.EventId, s.Status, s.HeldUntilUtc })
            .HasDatabaseName("IX_Seats_EventId_Status_HeldUntilUtc");

        e.HasIndex(s => new { s.Status, s.HeldUntilUtc })
            .HasDatabaseName("IX_Seats_Status_HeldUntilUtc")
            .HasFilter("[Status] = N'Held'");

        e.HasOne(s => s.Event)
            .WithMany(ev => ev.Seats)
            .HasForeignKey(s => s.EventId)
            .OnDelete(DeleteBehavior.Restrict);

        e.HasOne(s => s.HeldByUser)
            .WithMany(u => u.HeldSeats)
            .HasForeignKey(s => s.HeldByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
