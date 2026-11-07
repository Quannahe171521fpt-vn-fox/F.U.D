using FUD.DTOs.Keeping;
using FUD.Repositories.Interfaces.Keeping;
using FUD.Services.Interfaces.Keeping;

namespace FUD.Services.Keeping
{
    /// <summary>
    /// Kiểm tra điều kiện giữ phòng (UC02).
    ///
    /// Thứ tự kiểm tra (theo mức độ ưu tiên nghiệp vụ):
    ///   GB-06  → cư dân hiện tại (Residency Active kỳ hiện tại)
    ///   GB-04  → có kỳ Giữ phòng (Keeping) đang mở của Term kế tiếp
    ///   GB-16  → Term kế tiếp đã có bảng giá Published
    ///   GB-11  → không còn hóa đơn Overdue
    ///   GB-05  → không còn hóa đơn Unpaid
    ///           → chưa giữ phòng cho kỳ kế tiếp
    /// </summary>
    public class KeepingEligibilityService : IKeepingEligibilityService
    {
        private readonly IKeepingRepository _keepingRepository;

        public KeepingEligibilityService(IKeepingRepository keepingRepository)
        {
            _keepingRepository = keepingRepository;
        }

        public async Task<KeepingEligibilityResponseDto> CheckEligibilityAsync(int studentId)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)); // Giờ Việt Nam (UTC+7)

            // GB-06: Sinh viên phải là cư dân hiện tại.
            var currentResidency = await _keepingRepository.GetActiveResidencyAsync(studentId);
            if (currentResidency == null)
            {
                return Ineligible(
                    KeepingReasonCodes.NotResident,
                    "Bạn không phải là cư dân hiện tại nên không thể giữ phòng cho học kỳ kế tiếp.");
            }

            var currentStay = MapCurrentStay(currentResidency);

            // GB-04: Chỉ trong kỳ Giữ phòng (Keeping) đang Active của Term kế tiếp.
            var (period, nextTerm, currentTerm) =
                await _keepingRepository.GetOpenKeepingPeriodAsync(studentId, today);

            if (period == null || nextTerm == null)
            {
                return Ineligible(
                    KeepingReasonCodes.PeriodNotOpen,
                    "Hiện không có kỳ giữ phòng nào đang mở cho học kỳ kế tiếp.",
                    currentStay);
            }

            var nextTermDto = new NextTermDto
            {
                Id = nextTerm.Id,
                Name = nextTerm.Name,
                AcademicYear = nextTerm.AcademicYear,
                StartDate = nextTerm.StartDate,
                EndDate = nextTerm.EndDate
            };

            var periodDto = new KeepingPeriodDto
            {
                Start = period.StartDate,
                End = period.EndDate
            };

            // Phí giữ phòng: lấy từ Bed_Price của giường hiện tại trong kỳ kế tiếp (không hard-code).
            var unitPrice = await _keepingRepository.GetKeepingBedPriceAsync(currentResidency.BedId, nextTerm.Id);
            var months = await _keepingRepository.GetKeepingMonthsAsync(currentResidency.BedId, nextTerm.Id);
            var fee = new KeepingFeeDto
            {
                Amount = unitPrice ?? 0m,
                Currency = "VND",
                Months = months,
                Description = $"Phí giữ phòng {currentResidency.Bed?.BedCode} - {nextTerm.Name}"
            };

            // GB-16: Term kế tiếp phải có bảng giá Published.
            if (!await _keepingRepository.IsPriceListPublishedAsync(nextTerm.Id))
            {
                return Ineligible(
                    KeepingReasonCodes.PriceNotPublished,
                    "Bảng giá của học kỳ kế tiếp chưa được công bố nên chưa thể giữ phòng.",
                    currentStay, nextTermDto, fee, periodDto);
            }

            // GB-11: còn hóa đơn quá hạn → từ chối.
            if (await _keepingRepository.HasOverdueInvoiceAsync(studentId))
            {
                return Ineligible(
                    KeepingReasonCodes.HasOverdueInvoice,
                    "Bạn đang có hóa đơn quá hạn. Vui lòng thanh toán trước khi giữ phòng.",
                    currentStay, nextTermDto, fee, periodDto);
            }

            // GB-05: còn hóa đơn chưa thanh toán → từ chối.
            if (await _keepingRepository.HasUnpaidInvoiceAsync(studentId))
            {
                return Ineligible(
                    KeepingReasonCodes.HasUnpaidInvoice,
                    "Bạn đang có hóa đơn chưa thanh toán. Vui lòng thanh toán trước khi giữ phòng.",
                    currentStay, nextTermDto, fee, periodDto);
            }

            // Đã giữ phòng cho kỳ kế tiếp → từ chối.
            if (await _keepingRepository.HasCompletedKeepingRequestAsync(studentId, nextTerm.Id))
            {
                return Ineligible(
                    KeepingReasonCodes.AlreadyKept,
                    "Bạn đã giữ phòng cho học kỳ kế tiếp.",
                    currentStay, nextTermDto, fee, periodDto);
            }

            return new KeepingEligibilityResponseDto
            {
                Eligible = true,
                CurrentStay = currentStay,
                NextTerm = nextTermDto,
                Fee = fee,
                Period = periodDto
            };
        }

        private static CurrentStayDto MapCurrentStay(Models.Residency residency)
        {
            var bed = residency.Bed;
            var room = bed?.Room;
            var block = room?.Block;
            var dorm = block?.Dorm;

            return new CurrentStayDto
            {
                BedId = residency.BedId,
                DormName = dorm?.Name ?? string.Empty,
                BlockName = block?.Name ?? string.Empty,
                RoomNumber = room?.RoomNumber ?? string.Empty,
                BedCode = bed?.BedCode ?? string.Empty,
                CurrentTermId = residency.TermId,
                CurrentTerm = residency.Term?.Name ?? string.Empty,
                Status = residency.Status
            };
        }

        private static KeepingEligibilityResponseDto Ineligible(
            string reasonCode,
            string message,
            CurrentStayDto? currentStay = null,
            NextTermDto? nextTerm = null,
            KeepingFeeDto? fee = null,
            KeepingPeriodDto? period = null)
        {
            return new KeepingEligibilityResponseDto
            {
                Eligible = false,
                ReasonCode = reasonCode,
                Message = message,
                CurrentStay = currentStay,
                NextTerm = nextTerm,
                Fee = fee,
                Period = period
            };
        }
    }
}
