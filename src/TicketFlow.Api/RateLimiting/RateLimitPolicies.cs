namespace TicketFlow.Api.RateLimiting;

/// <summary>Tên policy rate limit dùng chung giữa Program.cs và Controllers.</summary>
public static class RateLimitPolicies
{
    /// <summary>Fixed window theo IP cho auth endpoints (login/register/refresh).</summary>
    public const string Auth = "AuthPolicy";

    /// <summary>Fixed window theo UserId (fallback IP) cho booking endpoints (hold/confirm).</summary>
    public const string Booking = "BookingPolicy";
}
