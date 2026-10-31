using System.ComponentModel.DataAnnotations;

namespace FUD.DTOs.Pricing
{
    public class TermLookupDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string AcademicYear { get; set; } = null!;
        public string DisplayName => $"{Name} ({AcademicYear})";
        public DateOnly StartDate { get; set; }
        public DateOnly EndDate { get; set; }
        public bool IsRegistrationStarted { get; set; }
        public string RegistrationStatus { get; set; } = "NotStarted"; // "NotStarted" | "Open" | "Closed"
    }

    public class PricingItemDto
    {
        public int DormId { get; set; }
        public string DormName { get; set; } = null!;
        public string DormCode { get; set; } = null!;

        public int RoomTypeId { get; set; }
        public string RoomTypeName { get; set; } = null!;
        public int BedCapacity { get; set; }

        public string BedPosition { get; set; } = "Standard"; // Standard / Giường tiêu chuẩn hoặc vị trí

        public decimal? PreviousSemesterPrice { get; set; }
        public decimal CurrentPrice { get; set; }
        public string Unit { get; set; } = "VND/học kỳ";
    }

    public class TermPricingResponseDto
    {
        public int TermId { get; set; }
        public string TermName { get; set; } = null!;
        public string AcademicYear { get; set; } = null!;
        public string Status { get; set; } = "Draft"; // Draft | Published
        public bool IsRegistrationStarted { get; set; }
        public string RegistrationStatus { get; set; } = "Chưa bắt đầu"; // "Chưa bắt đầu" | "Đang mở đăng ký" | "Đã kết thúc"
        public DateTime? PublishedTime { get; set; }
        public List<PricingItemDto> Items { get; set; } = new();
    }

    public class UpdatePricingItemInputDto
    {
        [Required]
        public int DormId { get; set; }

        [Required]
        public int RoomTypeId { get; set; }

        [Required]
        [Range(1, double.MaxValue, ErrorMessage = "Giá phải là số lớn hơn 0")]
        public decimal CurrentPrice { get; set; }
    }

    public class SavePricingRequestDto
    {
        [Required]
        public int TermId { get; set; }

        [Required]
        [MinLength(1, ErrorMessage = "Danh sách giá không được để trống")]
        public List<UpdatePricingItemInputDto> Items { get; set; } = new();

        public bool ConfirmApplyToNewBookingsOnly { get; set; } = false;
    }
}

