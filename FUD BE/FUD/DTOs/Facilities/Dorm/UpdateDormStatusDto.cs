using System.ComponentModel.DataAnnotations;

namespace FUD.DTOs.Facilities.Dorm
{
    /// <summary>
    /// Payload để cập nhật Status của Dorm.
    /// </summary>
    public class UpdateDormStatusDto
    {
        [Required(ErrorMessage = "Status là bắt buộc")]
        [RegularExpression("^(Active|Inactive)$", ErrorMessage = "Status phải là 'Active' hoặc 'Inactive'")]
        public string Status { get; set; } = string.Empty;
    }
}
