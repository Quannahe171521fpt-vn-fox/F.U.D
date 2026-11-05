using System.ComponentModel.DataAnnotations;

namespace FUD.DTOs.Pricing
{
    public class RegistrationPeriodDto
    {
        public int? Id { get; set; }
        public int TermId { get; set; }
        public string PeriodType { get; set; } = null!; // "Keeping" | "Booking"
        public DateOnly StartDate { get; set; }
        public DateOnly EndDate { get; set; }
        public bool ActiveFlag { get; set; }
        public string StatusText { get; set; } = "Chưa cấu hình"; // "Chưa cấu hình" | "Chưa bắt đầu" | "Đang mở" | "Đã kết thúc" | "Đã tắt"
    }

    public class TermSummaryDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string AcademicYear { get; set; } = null!;
        public string DisplayName => $"{Name} ({AcademicYear})";
        public DateOnly StartDate { get; set; }
        public DateOnly EndDate { get; set; }
        public string TermStatus { get; set; } = "Upcoming"; // "Upcoming" | "Ongoing" | "Ended"
        
        // Trạng thái bảng giá của kỳ
        public string PriceListStatus { get; set; } = "NotCreated"; // "NotCreated" | "Draft" | "Published"
        public DateTime? PriceListPublishedTime { get; set; }
        public bool IsPricePublished => PriceListStatus == "Published";

        // Hai giai đoạn đăng ký phòng
        public RegistrationPeriodDto? KeepingPeriod { get; set; }
        public RegistrationPeriodDto? BookingPeriod { get; set; }

        public bool HasDependentData { get; set; }
    }

    public class TermDetailDto : TermSummaryDto
    {
        public List<RegistrationPeriodDto> AllPeriods { get; set; } = new();
    }

    public class CreateTermRequestDto
    {
        [Required(ErrorMessage = "Tên học kỳ không được để trống")]
        [StringLength(100, ErrorMessage = "Tên học kỳ không quá 100 ký tự")]
        public string Name { get; set; } = null!;

        [Required(ErrorMessage = "Năm học không được để trống (Ví dụ: 2026-2027)")]
        [StringLength(50, ErrorMessage = "Năm học không quá 50 ký tự")]
        public string AcademicYear { get; set; } = null!;

        [Required(ErrorMessage = "Ngày bắt đầu học kỳ không được để trống")]
        public DateOnly StartDate { get; set; }

        [Required(ErrorMessage = "Ngày kết thúc học kỳ không được để trống")]
        public DateOnly EndDate { get; set; }
    }

    public class UpdateTermRequestDto
    {
        [Required(ErrorMessage = "Tên học kỳ không được để trống")]
        [StringLength(100, ErrorMessage = "Tên học kỳ không quá 100 ký tự")]
        public string Name { get; set; } = null!;

        [Required(ErrorMessage = "Năm học không được để trống")]
        [StringLength(50, ErrorMessage = "Năm học không quá 50 ký tự")]
        public string AcademicYear { get; set; } = null!;

        [Required(ErrorMessage = "Ngày bắt đầu học kỳ không được để trống")]
        public DateOnly StartDate { get; set; }

        [Required(ErrorMessage = "Ngày kết thúc học kỳ không được để trống")]
        public DateOnly EndDate { get; set; }
    }

    public class PeriodConfigItemDto
    {
        public int? Id { get; set; }

        [Required(ErrorMessage = "Loại giai đoạn không được để trống")]
        [RegularExpression("^(Keeping|Booking)$", ErrorMessage = "Loại giai đoạn phải là 'Keeping' hoặc 'Booking'")]
        public string PeriodType { get; set; } = null!;

        [Required(ErrorMessage = "Ngày bắt đầu không được để trống")]
        public DateOnly StartDate { get; set; }

        [Required(ErrorMessage = "Ngày kết thúc không được để trống")]
        public DateOnly EndDate { get; set; }

        public bool ActiveFlag { get; set; } = false;
    }

    public class SaveRegistrationScheduleRequestDto
    {
        [Required]
        public int TermId { get; set; }

        [Required]
        [MinLength(1, ErrorMessage = "Cần cung cấp ít nhất một giai đoạn đăng ký")]
        public List<PeriodConfigItemDto> Periods { get; set; } = new();
    }
}
