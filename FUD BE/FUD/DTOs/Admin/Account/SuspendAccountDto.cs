using System.ComponentModel.DataAnnotations;
using FUD.Common;

namespace FUD.DTOs.Admin.Account
{
    /// <summary>Body cho POST /api/admin/users/{id}/suspend</summary>
    public class SuspendAccountDto
    {
        /// <summary>Violation | Graduation | Termination | Other</summary>
        [Required(ErrorMessage = "Lý do tạm ngưng là bắt buộc")]
        [CustomValidation(typeof(AccountValidation), nameof(AccountValidation.ValidSuspensionReason))]
        public string Reason { get; set; } = string.Empty;

        /// <summary>Ghi chú chi tiết (bắt buộc khi Reason = Other).</summary>
        [StringLength(1000, ErrorMessage = "Ghi chú không vượt quá 1000 ký tự")]
        public string? Notes { get; set; }
    }
}
