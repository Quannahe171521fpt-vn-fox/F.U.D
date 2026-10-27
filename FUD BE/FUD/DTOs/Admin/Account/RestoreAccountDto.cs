using System.ComponentModel.DataAnnotations;

namespace FUD.DTOs.Admin.Account
{
    /// <summary>Body cho POST /api/admin/users/{id}/restore</summary>
    public class RestoreAccountDto
    {
        [Required(ErrorMessage = "Lý do khôi phục là bắt buộc")]
        [StringLength(500, MinimumLength = 5, ErrorMessage = "Lý do khôi phục phải từ 5 đến 500 ký tự")]
        public string Reason { get; set; } = string.Empty;
    }
}
