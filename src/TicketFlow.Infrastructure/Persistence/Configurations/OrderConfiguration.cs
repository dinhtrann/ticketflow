using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TicketFlow.Domain.Entities;

namespace TicketFlow.Infrastructure.Persistence.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> e)
    {
        e.ToTable("Orders", t => t.HasCheckConstraint("CK_Orders_TotalAmount_NonNegative", "[TotalAmount] >= 0"));
        e.HasKey(o => o.Id);

        e.Property(o => o.TotalAmount).HasPrecision(18, 2);
        e.Property(o => o.Status)
            .HasConversion<string>()
            .HasMaxLength(20);
        e.Property(o => o.IdempotencyKey).HasMaxLength(128).IsRequired();
        e.Property(o => o.RequestHash)
            .HasMaxLength(64)
            .IsFixedLength()
            .IsUnicode(false)
            .IsRequired();
        e.Property(o => o.CreatedAtUtc).IsRequired();

        e.HasIndex(o => new { o.UserId, o.IdempotencyKey })
            .IsUnique()
            .HasDatabaseName("UX_Orders_UserId_IdempotencyKey");

        e.HasIndex(o => o.SeatId)
            .IsUnique()
            .HasDatabaseName("UX_Orders_SeatId_Confirmed")
            .HasFilter("[Status] = N'Confirmed'");

        e.HasIndex(o => new { o.UserId, o.CreatedAtUtc })
            .IsDescending(false, true)
            .HasDatabaseName("IX_Orders_UserId_CreatedAtUtc");

        e.HasOne(o => o.User)
            .WithMany(u => u.Orders)
            .HasForeignKey(o => o.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        e.HasOne(o => o.Event)
            .WithMany(ev => ev.Orders)
            .HasForeignKey(o => o.EventId)
            .OnDelete(DeleteBehavior.Restrict);

        e.HasOne(o => o.Seat)
            .WithOne(s => s.Order)
            .HasForeignKey<Order>(o => o.SeatId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
