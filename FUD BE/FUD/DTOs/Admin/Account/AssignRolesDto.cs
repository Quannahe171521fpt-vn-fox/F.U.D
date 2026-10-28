using System.ComponentModel.DataAnnotations;
using FUD.Common;

namespace FUD.DTOs.Admin.Account
{
    /// <summary>Body cho PUT /api/admin/users/{id}/roles — Cấp / đổi / thu hồi vai trò</summary>
    public class AssignRolesDto
    {
        /// <summary>
        /// Danh sách vai trò mong muốn sau khi cập nhật (replace toàn bộ).
        /// Mảng rỗng => thu hồi tất cả vai trò.
        /// </summary>
        [Required(ErrorMessage = "Danh sách vai trò là bắt buộc")]
        [MinLength(1, ErrorMessage = "Phải giữ lại ít nhất một vai trò")]
        public List<string> RoleNames { get; set; } = new List<string>();
    }

    /// <summary>
    /// Validator dùng chung cho các request DTO của module Account Management.
    /// </summary>
    public static class AccountValidation
    {
        public static ValidationResult? ValidRoleName(string? value, ValidationContext context)
        {
            if (string.IsNullOrWhiteSpace(value))
                return new ValidationResult("Vai trò không được để trống");

            if (!AccountConstants.AllRoles.Contains(value))
                return new ValidationResult(
                    $"Vai trò '{value}' không hợp lệ. Chỉ chấp nhận: {string.Join(", ", AccountConstants.AllRoles)}");

            return ValidationResult.Success;
        }

        public static ValidationResult? ValidSuspensionReason(string? value, ValidationContext context)
        {
            if (string.IsNullOrWhiteSpace(value))
                return new ValidationResult("Lý do tạm ngưng là bắt buộc");

            if (!AccountConstants.AllSuspensionReasons.Contains(value))
                return new ValidationResult(
                    $"Lý do '{value}' không hợp lệ. Chỉ chấp nhận: {string.Join(", ", AccountConstants.AllSuspensionReasons)}");

            return ValidationResult.Success;
        }
    }
}
