using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using TicketFlow.Infrastructure.Persistence;

namespace TicketFlow.Infrastructure;

/// <summary>
/// Design-time factory cho `dotnet ef` — đọc connection string từ env
/// `ConnectionStrings__Default`.
/// </summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
                        ?? "Your_Connection_String";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new AppDbContext(options);
    }
}
