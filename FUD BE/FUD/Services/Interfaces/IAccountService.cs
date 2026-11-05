using FUD.DTOs.Account;
using FUD.Validators.Account;
using FluentValidation;

namespace FUD.Services.Interfaces;

/// <summary>Service Layer cho module Account Management (Create / Assign Roles / Suspend / Restore).</summary>
public interface IAccountService
{
    /// <summary>Danh sách tài khoản có tìm kiếm, lọc và phân trang.</summary>
    Task<FUD.Common.PagedResult<AccountDto>> GetAllAsync(AccountQueryDto query);

    /// <summary>Chi tiết tài khoản kèm lịch sử trạng thái và vai trò.</summary>
    Task<AccountDetailDto?> GetByIdAsync(int userId);

    /// <summary>Tạo tài khoản mới (mặc định trạng thái Active).</summary>
    Task<AccountDto> CreateAsync(CreateAccountDto dto, int adminUserId);

    /// <summary>Cấp / chỉnh sửa / thu hồi vai trò cho tài khoản.</summary>
    Task<AccountDto> AssignRolesAsync(int userId, AssignRolesDto dto, int adminUserId);

    /// <summary>Tạm ngưng tài khoản (soft deactivation, không xóa bản ghi).</summary>
    Task<AccountDto> SuspendAsync(int userId, SuspendAccountDto dto, int adminUserId);

    /// <summary>Khôi phục tài khoản đang bị tạm ngưng.</summary>
    Task<AccountDto> RestoreAsync(int userId, RestoreAccountDto dto, int adminUserId);
}

/// <summary>Đăng ký FluentValidator cho các request DTO của module Account Management.</summary>
public static class AccountValidatorRegistration
{
    public static IServiceCollection AddAccountValidators(this IServiceCollection services)
    {
        services.AddScoped<IValidator<CreateAccountDto>, CreateAccountValidator>();
        services.AddScoped<IValidator<AssignRolesDto>, AssignRolesValidator>();
        services.AddScoped<IValidator<SuspendAccountDto>, SuspendAccountValidator>();
        services.AddScoped<IValidator<RestoreAccountDto>, RestoreAccountValidator>();
        return services;
    }
}
