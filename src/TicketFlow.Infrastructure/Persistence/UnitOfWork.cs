using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TicketFlow.Application.Interfaces;
using TicketFlow.Domain.Exceptions;
using TicketFlow.Infrastructure.Persistence;

namespace TicketFlow.Infrastructure.Persistence;

public sealed partial class UnitOfWork(AppDbContext db) : IUnitOfWork
{
    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        try
        {
            return await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException(ex.Message);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            throw new UniqueConstraintViolationException(ExtractConstraintName(ex) ?? "UNKNOWN", ex.Message);
        }
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        for (var inner = ex.InnerException; inner is not null; inner = inner.InnerException)
        {
            if (inner is SqlException { Number: 2601 or 2627 })
            {
                return true;
            }
        }
        return false;
    }

    private static string? ExtractConstraintName(DbUpdateException ex)
    {
        for (var inner = ex.InnerException; inner is not null; inner = inner.InnerException)
        {
            if (inner is not SqlException sql)
            {
                continue;
            }
            var match = ConstraintNameRegex().Match(sql.Message);
            if (match.Success)
            {
                return match.Groups[1].Value;
            }
        }
        return null;
    }

    [GeneratedRegex("(?:UNIQUE KEY constraint|unique index|UNIQUE INDEX|PRIMARY KEY constraint) '([^']+)'")]
    private static partial Regex ConstraintNameRegex();
}
