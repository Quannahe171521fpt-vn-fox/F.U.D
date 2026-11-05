using FUD.DTOs.Pricing;
using FUD.Models;
using FUD.Repositories.Interfaces;
using FUD.Services.Interfaces;

namespace FUD.Services
{
    public class TermService : ITermService
    {
        private readonly ITermRepository _termRepository;

        public TermService(ITermRepository termRepository)
        {
            _termRepository = termRepository;
        }

        public async Task<List<TermSummaryDto>> GetAllTermsAsync()
        {
            var terms = await _termRepository.GetAllTermsAsync();
            var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)); // Giờ Việt Nam (UTC+7)

            var result = new List<TermSummaryDto>();

            foreach (var term in terms)
            {
                var hasDeps = await _termRepository.HasDependentDataAsync(term.Id);
                result.Add(MapToSummaryDto(term, today, hasDeps));
            }

            return result;
        }

        public async Task<TermDetailDto> GetTermDetailAsync(int termId)
        {
            var term = await _termRepository.GetTermByIdAsync(termId);
            if (term == null)
            {
                throw new KeyNotFoundException($"Không tìm thấy học kỳ với mã {termId}");
            }

            var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
            var hasDeps = await _termRepository.HasDependentDataAsync(term.Id);
            var summary = MapToSummaryDto(term, today, hasDeps);

            var detail = new TermDetailDto
            {
                Id = summary.Id,
                Name = summary.Name,
                AcademicYear = summary.AcademicYear,
                StartDate = summary.StartDate,
                EndDate = summary.EndDate,
                TermStatus = summary.TermStatus,
                PriceListStatus = summary.PriceListStatus,
                PriceListPublishedTime = summary.PriceListPublishedTime,
                KeepingPeriod = summary.KeepingPeriod,
                BookingPeriod = summary.BookingPeriod,
                HasDependentData = summary.HasDependentData,
                AllPeriods = term.RegistrationPeriods.Select(p => MapToPeriodDto(p, today)).ToList()
            };

            return detail;
        }

        public async Task<TermDetailDto> CreateTermAsync(CreateTermRequestDto dto)
        {
            if (dto.EndDate <= dto.StartDate)
            {
                throw new ArgumentException("Ngày kết thúc học kỳ phải lớn hơn ngày bắt đầu.");
            }

            var trimmedName = dto.Name.Trim();
            var trimmedYear = dto.AcademicYear.Trim();

            var exists = await _termRepository.ExistsTermAsync(trimmedName, trimmedYear);
            if (exists)
            {
                throw new ArgumentException($"Học kỳ '{trimmedName}' cho năm học '{trimmedYear}' đã tồn tại.");
            }

            var term = new Term
            {
                Name = trimmedName,
                AcademicYear = trimmedYear,
                StartDate = dto.StartDate,
                EndDate = dto.EndDate
            };

            var created = await _termRepository.CreateTermAsync(term);
            return await GetTermDetailAsync(created.Id);
        }

        public async Task<TermDetailDto> UpdateTermAsync(int termId, UpdateTermRequestDto dto)
        {
            var term = await _termRepository.GetTermByIdAsync(termId);
            if (term == null)
            {
                throw new KeyNotFoundException($"Không tìm thấy học kỳ với mã {termId}");
            }

            if (dto.EndDate <= dto.StartDate)
            {
                throw new ArgumentException("Ngày kết thúc học kỳ phải lớn hơn ngày bắt đầu.");
            }

            var trimmedName = dto.Name.Trim();
            var trimmedYear = dto.AcademicYear.Trim();

            var exists = await _termRepository.ExistsTermAsync(trimmedName, trimmedYear, termId);
            if (exists)
            {
                throw new ArgumentException($"Học kỳ '{trimmedName}' cho năm học '{trimmedYear}' đã tồn tại.");
            }

            term.Name = trimmedName;
            term.AcademicYear = trimmedYear;
            term.StartDate = dto.StartDate;
            term.EndDate = dto.EndDate;

            await _termRepository.UpdateTermAsync(term);
            return await GetTermDetailAsync(termId);
        }

        public async Task DeleteTermAsync(int termId)
        {
            var term = await _termRepository.GetTermByIdAsync(termId);
            if (term == null)
            {
                throw new KeyNotFoundException($"Không tìm thấy học kỳ với mã {termId}");
            }

            var hasDeps = await _termRepository.HasDependentDataAsync(termId);
            if (hasDeps)
            {
                throw new InvalidOperationException("Không thể xóa học kỳ vì đã có dữ liệu sinh viên đăng ký, cư trú hoặc hóa đơn liên quan.");
            }

            await _termRepository.DeleteTermAsync(termId);
        }

        public async Task<TermDetailDto> SaveRegistrationScheduleAsync(int termId, SaveRegistrationScheduleRequestDto dto)
        {
            var term = await _termRepository.GetTermByIdAsync(termId);
            if (term == null)
            {
                throw new KeyNotFoundException($"Không tìm thấy học kỳ với mã {termId}");
            }

            // 1. Kiểm tra ngày từng giai đoạn
            foreach (var p in dto.Periods)
            {
                if (p.EndDate <= p.StartDate)
                {
                    var periodName = p.PeriodType == "Keeping" ? "Giữ phòng cũ" : "Đăng ký mới";
                    throw new ArgumentException($"Ngày kết thúc của đợt {periodName} phải lớn hơn ngày bắt đầu.");
                }
            }

            // 2. Quy tắc nghiệp vụ 1: EndDate_Keeping <= StartDate_Booking
            var keeping = dto.Periods.FirstOrDefault(p => p.PeriodType == "Keeping");
            var booking = dto.Periods.FirstOrDefault(p => p.PeriodType == "Booking");
            if (keeping != null && booking != null)
            {
                if (keeping.EndDate > booking.StartDate)
                {
                    throw new ArgumentException("Giai đoạn Giữ phòng cũ (Keeping) phải kết thúc trước hoặc cùng ngày khi giai đoạn Đăng ký phòng mới (Booking) bắt đầu.");
                }
            }

            // 3. Quy tắc nghiệp vụ 2: Tuân thủ Trigger trg_RegPeriod_RequirePublishedPrice
            var anyActive = dto.Periods.Any(p => p.ActiveFlag);
            if (anyActive)
            {
                var isPricePublished = await _termRepository.IsPriceListPublishedAsync(termId);
                if (!isPricePublished)
                {
                    throw new InvalidOperationException("Không thể kích hoạt đợt đăng ký: kỳ học chưa có bảng giá ở trạng thái Published. Vui lòng thiết lập và công bố bảng giá trước.");
                }
            }

            var entities = dto.Periods.Select(p => new RegistrationPeriod
            {
                Id = p.Id ?? 0,
                TermId = termId,
                PeriodType = p.PeriodType,
                StartDate = p.StartDate,
                EndDate = p.EndDate,
                ActiveFlag = p.ActiveFlag
            }).ToList();

            await _termRepository.SaveRegistrationPeriodsAsync(termId, entities);

            return await GetTermDetailAsync(termId);
        }

        public async Task TogglePeriodActiveAsync(int termId, int periodId, bool active)
        {
            if (active)
            {
                var isPricePublished = await _termRepository.IsPriceListPublishedAsync(termId);
                if (!isPricePublished)
                {
                    throw new InvalidOperationException("Không thể kích hoạt đợt đăng ký: kỳ học chưa có bảng giá ở trạng thái Published.");
                }
            }

            await _termRepository.TogglePeriodActiveAsync(periodId, active);
        }

        private static TermSummaryDto MapToSummaryDto(Term term, DateOnly today, bool hasDeps)
        {
            string termStatus;
            if (today < term.StartDate)
            {
                termStatus = "Upcoming";
            }
            else if (today >= term.StartDate && today <= term.EndDate)
            {
                termStatus = "Ongoing";
            }
            else
            {
                termStatus = "Ended";
            }

            string priceStatus = "NotCreated";
            DateTime? publishedTime = null;
            if (term.PriceList != null)
            {
                priceStatus = term.PriceList.Status == "Published" ? "Published" : "Draft";
                publishedTime = term.PriceList.PublishedTime;
            }

            var keeping = term.RegistrationPeriods.FirstOrDefault(rp => rp.PeriodType == "Keeping");
            var booking = term.RegistrationPeriods.FirstOrDefault(rp => rp.PeriodType == "Booking");

            return new TermSummaryDto
            {
                Id = term.Id,
                Name = term.Name,
                AcademicYear = term.AcademicYear,
                StartDate = term.StartDate,
                EndDate = term.EndDate,
                TermStatus = termStatus,
                PriceListStatus = priceStatus,
                PriceListPublishedTime = publishedTime,
                KeepingPeriod = keeping != null ? MapToPeriodDto(keeping, today) : null,
                BookingPeriod = booking != null ? MapToPeriodDto(booking, today) : null,
                HasDependentData = hasDeps
            };
        }

        private static RegistrationPeriodDto MapToPeriodDto(RegistrationPeriod period, DateOnly today)
        {
            string statusText;
            if (!period.ActiveFlag)
            {
                statusText = "Đã tắt";
            }
            else if (today < period.StartDate)
            {
                statusText = "Chưa bắt đầu";
            }
            else if (today >= period.StartDate && today <= period.EndDate)
            {
                statusText = "Đang mở";
            }
            else
            {
                statusText = "Đã kết thúc";
            }

            return new RegistrationPeriodDto
            {
                Id = period.Id,
                TermId = period.TermId,
                PeriodType = period.PeriodType,
                StartDate = period.StartDate,
                EndDate = period.EndDate,
                ActiveFlag = period.ActiveFlag,
                StatusText = statusText
            };
        }
    }
}
