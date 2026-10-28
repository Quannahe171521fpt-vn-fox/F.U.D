using FUD.Common;
using FUD.DTOs.Admin.Account;

namespace FUD.Services.Interfaces.Accounts
{
    public interface IAccountService
    {
        /// <summary>Danh sách tài khoản có tìm kiếm, lọc và phân trang.</summary>
        Task<PagedResult<AccountListItemDto>> GetAllAsync(AccountQueryDto query);

        /// <summary>Chi tiết tài khoản kèm lịch sử trạng thái và lịch sử vai trò.</summary>
        Task<AccountDetailDto?> GetByIdAsync(int id);

        /// <summary>Danh sách vai trò hệ thống có thể cấp.</summary>
        Task<List<RoleOptionDto>> GetRoleOptionsAsync();

        /// <summary>Tạo tài khoản mới (Admin thực hiện thủ công).</summary>
        Task<AccountDetailDto> CreateAsync(CreateAccountDto dto, int adminId);

        /// <summary>Cấp / đổi / thu hồi vai trò.</summary>
        Task<AccountDetailDto> AssignRolesAsync(int userId, AssignRolesDto dto, int adminId);

        /// <summary>Tạm ngưng tài khoản (soft deactivate, không xóa bản ghi).</summary>
        Task<AccountStatusResultDto> SuspendAsync(int userId, SuspendAccountDto dto, int adminId);

        /// <summary>Khôi phục tài khoản đang tạm ngưng.</summary>
        Task<AccountStatusResultDto> RestoreAsync(int userId, RestoreAccountDto dto, int adminId);
    }
}
