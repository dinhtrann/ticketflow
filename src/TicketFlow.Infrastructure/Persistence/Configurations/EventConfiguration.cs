using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TicketFlow.Domain.Entities;

namespace TicketFlow.Infrastructure.Persistence.Configurations;

public class EventConfiguration : IEntityTypeConfiguration<Event>
{
    public void Configure(EntityTypeBuilder<Event> e)
    {
        e.ToTable("Events");
        e.HasKey(x => x.Id);

        e.Property(x => x.Title).HasMaxLength(200).IsRequired();
        e.Property(x => x.Description).HasMaxLength(2000);
        e.Property(x => x.Venue).HasMaxLength(200).IsRequired();
        e.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(20);
        e.Property(x => x.StartsAtUtc).IsRequired();
        e.Property(x => x.CreatedAtUtc).IsRequired();
        e.Property(x => x.UpdatedAtUtc).IsRequired();

        e.HasIndex(x => new { x.Status, x.StartsAtUtc })
            .HasDatabaseName("IX_Events_Status_StartsAtUtc");
    }
}
