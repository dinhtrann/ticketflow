using System.Diagnostics;
using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using TicketFlow.Domain.Exceptions;

namespace TicketFlow.Api.Exceptions;

public sealed class GlobalExceptionHandler(
    IHostEnvironment environment,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    private static readonly IReadOnlyDictionary<string, int> CodeToStatus = new Dictionary<string, int>
    {
        ["VALIDATION_ERROR"] = 400,
        ["INVALID_STATUS_TRANSITION"] = 400,
        ["INVALID_CREDENTIALS"] = 401,
        ["INVALID_REFRESH_TOKEN"] = 401,
        ["TOKEN_REUSE_DETECTED"] = 401,
        ["UNAUTHORIZED"] = 401,
        ["FORBIDDEN"] = 403,
        ["EVENT_NOT_FOUND"] = 404,
        ["SEAT_NOT_FOUND"] = 404,
        ["ORDER_NOT_FOUND"] = 404,
        ["EMAIL_ALREADY_EXISTS"] = 409,
        ["DUPLICATE_SEAT_CODE"] = 409,
        ["EVENT_NOT_BOOKABLE"] = 409,
        ["SEAT_ALREADY_HELD"] = 409,
        ["SEAT_ALREADY_SOLD"] = 409,
        ["SEAT_HOLD_EXPIRED"] = 409,
        ["SEAT_NOT_HELD_BY_USER"] = 409,
        ["SEAT_NOT_HELD"] = 409,
        ["SEAT_CONCURRENCY_CONFLICT"] = 409,
        ["IDEMPOTENCY_KEY_PAYLOAD_MISMATCH"] = 422,
        ["RATE_LIMIT_EXCEEDED"] = 429,
        ["INTERNAL_SERVER_ERROR"] = 500,
    };

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var code = ResolveCode(exception);
        var status = CodeToStatus.GetValueOrDefault(code, FallbackStatus(exception));
        var isServerError = status >= 500;

        if (isServerError)
        {
            logger.LogError(exception, "Unhandled exception while processing {Method} {Path}",
                httpContext.Request.Method, httpContext.Request.Path);
        }

        var problem = new ProblemDetails
        {
            Type = $"https://ticketflow.local/errors/{code.ToLowerInvariant().Replace('_', '-')}",
            Title = ToTitle(code),
            Status = status,
            Detail = isServerError && !environment.IsDevelopment()
                ? "An unexpected error occurred."
                : exception.Message,
            Instance = httpContext.Request.Path.Value,
        };
        problem.Extensions["code"] = code;
        problem.Extensions["traceId"] = Activity.Current?.Id ?? httpContext.TraceIdentifier;

        if (exception is ValidationException validationException)
        {
            problem.Extensions["errors"] = validationException.Errors
                .GroupBy(e => string.IsNullOrEmpty(e.PropertyName) ? "general" : e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
        }

        httpContext.Response.StatusCode = status;
        httpContext.Response.ContentType = "application/problem+json";
        await JsonSerializer.SerializeAsync(httpContext.Response.Body, problem, cancellationToken: cancellationToken);
        return true;
    }

    private static string ResolveCode(Exception exception) => exception switch
    {
        ValidationException => "VALIDATION_ERROR",
        UniqueConstraintViolationException unique => unique.ConstraintName == "UX_Users_Email"
            ? "EMAIL_ALREADY_EXISTS"
            : unique.Code,
        TicketFlowException ticketFlow => ticketFlow.Code,
        _ => "INTERNAL_SERVER_ERROR",
    };

    private static int FallbackStatus(Exception exception) => exception switch
    {
        UnauthorizedException => 401,
        ForbiddenException => 403,
        NotFoundException => 404,
        DomainValidationException or ConcurrencyConflictException or UniqueConstraintViolationException => 409,
        _ => 500,
    };

    private static string ToTitle(string code)
    {
        var words = code.Split('_', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            return code;
        }

        return string.Join(' ', words.Select((w, i) =>
            i == 0
                ? char.ToUpperInvariant(w[0]) + w[1..].ToLowerInvariant()
                : w.ToLowerInvariant()));
    }
}
