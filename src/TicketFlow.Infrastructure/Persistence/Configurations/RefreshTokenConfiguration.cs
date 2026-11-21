using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TicketFlow.Domain.Entities;

namespace TicketFlow.Infrastructure.Persistence.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> e)
    {
        e.ToTable("RefreshTokens");
        e.HasKey(t => t.Id);

        e.Property(t => t.TokenHash)
            .HasMaxLength(64)
            .IsFixedLength()
            .IsUnicode(false)
            .IsRequired();
        e.HasIndex(t => t.TokenHash)
            .IsUnique()
            .HasDatabaseName("UX_RefreshTokens_TokenHash");

        e.Property(t => t.ReplacedByTokenHash)
            .HasMaxLength(64)
            .IsFixedLength()
            .IsUnicode(false);

        e.Property(t => t.ExpiresAtUtc).IsRequired();
        e.Property(t => t.CreatedAtUtc).IsRequired();
        e.Property(t => t.RevokedAtUtc);

        e.HasIndex(t => new { t.UserId, t.RevokedAtUtc })
            .HasDatabaseName("IX_RefreshTokens_UserId_RevokedAtUtc");

        e.HasOne(t => t.User)
            .WithMany(u => u.RefreshTokens)
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
