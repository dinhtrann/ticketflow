namespace TicketFlow.Application.Services;

public interface IAdminSeeder
{
    /// <summary>Tạo đúng 1 tài khoản Admin khi bảng Users đang trống.</summary>
    Task SeedAsync(CancellationToken ct = default);
}
