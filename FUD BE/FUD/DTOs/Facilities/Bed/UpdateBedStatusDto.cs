using System.ComponentModel.DataAnnotations;

namespace FUD.DTOs.Facilities.Bed
{
    /// <summary>Body cho PATCH api/beds/{id}/status — Chỉ cho phép Free hoặc Maintenance</summary>
    public class UpdateBedStatusDto
    {
        [Required(ErrorMessage = "Status là bắt buộc")]
        [RegularExpression("^(Free|Maintenance)$",
            ErrorMessage = "Status chỉ được là 'Free' hoặc 'Maintenance'")]
        public string Status { get; set; } = string.Empty;
    }
}

