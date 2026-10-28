using System.ComponentModel.DataAnnotations;

namespace FUD.DTOs.Admin.Account
{
    /// <summary>
    /// Query cho GET /api/admin/users — tìm kiếm, lọc theo trạng thái/vai trò, phân trang.
    /// </summary>
    public class AccountQueryDto
    {
        /// <summary>Tìm theo username, họ tên hoặc email.</summary>
        public string? Keyword { get; set; }

        /// <summary>Pending | Active | Suspended</summary>
        public string? Status { get; set; }

        /// <summary>Student | Manager | Guard | Admin</summary>
        public string? RoleName { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Page phải bắt đầu từ 1")]
        public int Page { get; set; } = 1;

        [Range(1, 100, ErrorMessage = "PageSize phải từ 1 đến 100")]
        public int PageSize { get; set; } = 10;
    }
}
