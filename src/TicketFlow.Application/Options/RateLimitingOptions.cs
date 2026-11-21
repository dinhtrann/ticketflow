namespace TicketFlow.Application.Options;

/// <summary>Cấu hình Rate Limiting (PRD §7). Bind từ section "RateLimiting" trong appsettings/Environment Variables.</summary>
public sealed class RateLimitingOptions
{
    /// <summary>AuthPolicy: số request tối đa cho /auth/login, /auth/register, /auth/refresh.</summary>
    public int AuthPermitLimit { get; set; } = 10;

    /// <summary>Cửa sổ thời gian (giây) của AuthPolicy — mặc định 60s = 1 phút.</summary>
    public int AuthWindowSeconds { get; set; } = 60;

    /// <summary>BookingPolicy: số request tối đa cho /seats/*/hold, /orders/confirm.</summary>
    public int BookingPermitLimit { get; set; } = 60;

    /// <summary>Cửa sổ thời gian (giây) của BookingPolicy — mặc định 60s = 1 phút.</summary>
    public int BookingWindowSeconds { get; set; } = 60;
}
