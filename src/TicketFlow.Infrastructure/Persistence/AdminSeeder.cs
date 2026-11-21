using Microsoft.Extensions.Logging;
using TicketFlow.Application.Interfaces;
using TicketFlow.Application.Options;
using TicketFlow.Application.Services;
using TicketFlow.Domain.Entities;
using TicketFlow.Domain.Enums;

namespace TicketFlow.Infrastructure.Persistence;

public class AdminSeeder(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    AdminOptions adminOptions,
    ILogger<AdminSeeder> logger) : IAdminSeeder
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(adminOptions.Email)
            || string.IsNullOrWhiteSpace(adminOptions.Password)
            || adminOptions.Password == "CHANGE_ME")
        {
            logger.LogWarning("Admin seed skipped: Admin:Email/Admin:Password is not configured.");
            return;
        }

        if (await userRepository.AnyAsync(ct))
        {
            return;
        }

        var admin = new User
        {
            Id = Guid.CreateVersion7(),
            Email = adminOptions.Email.Trim().ToLowerInvariant(),
            PasswordHash = passwordHasher.Hash(adminOptions.Password),
            FullName = "Administrator",
            Role = UserRole.Admin,
            CreatedAtUtc = timeProvider.GetUtcNow(),
        };

        userRepository.Add(admin);
        await unitOfWork.SaveChangesAsync(ct);
        logger.LogInformation("Seeded initial admin account {Email}.", admin.Email);
    }
}
