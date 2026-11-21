using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TicketFlow.Domain.Entities;

namespace TicketFlow.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> e)
    {
        e.ToTable("Users");
        e.HasKey(u => u.Id);

        e.Property(u => u.Email).HasMaxLength(256).IsRequired();
        e.HasIndex(u => u.Email)
            .IsUnique()
            .HasDatabaseName("UX_Users_Email");

        e.Property(u => u.PasswordHash).HasMaxLength(256).IsRequired();
        e.Property(u => u.FullName).HasMaxLength(150).IsRequired();
        e.Property(u => u.Role)
            .HasConversion<string>()
            .HasMaxLength(20);
        e.Property(u => u.CreatedAtUtc).IsRequired();
    }
}
