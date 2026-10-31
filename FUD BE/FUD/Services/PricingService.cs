using FUD.DTOs.Pricing;
using FUD.Models;
using FUD.Repositories.Interfaces;
using FUD.Services.Interfaces;

namespace FUD.Services
{
    public class PricingService : IPricingService
    {
        private readonly IPricingRepository _pricingRepository;

        public PricingService(IPricingRepository pricingRepository)
        {
            _pricingRepository = pricingRepository;
        }

        public async Task<List<TermLookupDto>> GetTermsLookupAsync()
        {
            var terms = await _pricingRepository.GetAllTermsAsync();
            var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)); // Giờ VN

            return terms.Select(t =>
            {
                var (isStarted, statusText) = DetermineRegistrationStatus(t, today);
                return new TermLookupDto
                {
                    Id = t.Id,
                    Name = t.Name,
                    AcademicYear = t.AcademicYear,
                    StartDate = t.StartDate,
                    EndDate = t.EndDate,
                    IsRegistrationStarted = isStarted,
                    RegistrationStatus = statusText
                };
            }).ToList();
        }

        public async Task<TermPricingResponseDto> GetPricingByTermAsync(int termId)
        {
            var term = await _pricingRepository.GetTermByIdAsync(termId);
            if (term == null)
            {
                throw new KeyNotFoundException($"Không tìm thấy học kỳ với mã {termId}");
            }

            var allTerms = await _pricingRepository.GetAllTermsAsync();
            var prevTerm = allTerms
                .Where(t => t.StartDate < term.StartDate)
                .OrderByDescending(t => t.StartDate)
                .FirstOrDefault();

            PriceList? prevPriceList = null;
            if (prevTerm != null)
            {
                prevPriceList = await _pricingRepository.GetPriceListByTermIdAsync(prevTerm.Id);
            }

            var currentPriceList = await _pricingRepository.GetPriceListByTermIdAsync(termId);
            var dorms = await _pricingRepository.GetAllDormsAsync();
            var roomTypes = await _pricingRepository.GetAllRoomTypesAsync();

            var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
            var (isRegistrationStarted, registrationStatus) = DetermineRegistrationStatus(term, today);

            var items = new List<PricingItemDto>();

            foreach (var dorm in dorms)
            {
                foreach (var rt in roomTypes)
                {
                    var existingCurrent = currentPriceList?.PriceItems
                        .FirstOrDefault(pi => pi.DormId == dorm.Id && pi.RoomTypeId == rt.Id);

                    var existingPrev = prevPriceList?.PriceItems
                        .FirstOrDefault(pi => pi.DormId == dorm.Id && pi.RoomTypeId == rt.Id);

                    items.Add(new PricingItemDto
                    {
                        DormId = dorm.Id,
                        DormName = dorm.Name,
                        DormCode = dorm.Code,
                        RoomTypeId = rt.Id,
                        RoomTypeName = rt.Name,
                        BedCapacity = rt.BedCapacity,
                        BedPosition = "Tất cả vị trí",
                        PreviousSemesterPrice = existingPrev?.UnitPrice,
                        CurrentPrice = existingCurrent?.UnitPrice ?? existingPrev?.UnitPrice ?? 0,
                        Unit = "VND/học kỳ"
                    });
                }
            }

            return new TermPricingResponseDto
            {
                TermId = term.Id,
                TermName = term.Name,
                AcademicYear = term.AcademicYear,
                Status = currentPriceList?.Status ?? "Draft",
                IsRegistrationStarted = isRegistrationStarted,
                RegistrationStatus = registrationStatus,
                PublishedTime = currentPriceList?.PublishedTime,
                Items = items
            };
        }

        public async Task<TermPricingResponseDto> SavePricingAsync(int managerUserId, SavePricingRequestDto request)
        {
            var term = await _pricingRepository.GetTermByIdAsync(request.TermId);
            if (term == null)
            {
                throw new KeyNotFoundException($"Không tìm thấy học kỳ với mã {request.TermId}");
            }

            var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
            var (isRegistrationStarted, _) = DetermineRegistrationStatus(term, today);

            // Business Rule A1: Giá phải là số lớn hơn 0
            foreach (var item in request.Items)
            {
                if (item.CurrentPrice <= 0)
                {
                    throw new ArgumentException("Giá không hợp lệ: Giá phải là số lớn hơn 0");
                }
            }

            // Business Rule A2: Nếu đợt đăng ký đã bắt đầu và client chưa gửi confirm
            if (isRegistrationStarted && !request.ConfirmApplyToNewBookingsOnly)
            {
                throw new InvalidOperationException("REGISTRATION_ALREADY_STARTED");
            }

            var priceList = await _pricingRepository.GetPriceListByTermIdAsync(request.TermId);
            if (priceList == null)
            {
                priceList = new PriceList
                {
                    TermId = request.TermId,
                    Status = "Published",
                    CreationDate = DateTime.UtcNow,
                    CreatedById = managerUserId,
                    PublishedTime = DateTime.UtcNow
                };
                priceList = await _pricingRepository.CreatePriceListAsync(priceList);
            }
            else
            {
                priceList.Status = "Published";
                priceList.PublishedTime = DateTime.UtcNow;
            }

            var priceItems = request.Items.Select(item => new PriceItem
            {
                PriceListId = priceList.Id,
                DormId = item.DormId,
                RoomTypeId = item.RoomTypeId,
                UnitPrice = item.CurrentPrice
            }).ToList();

            await _pricingRepository.UpdatePriceItemsAsync(priceList.Id, priceItems);

            return await GetPricingByTermAsync(request.TermId);
        }

        private static (bool isStarted, string statusText) DetermineRegistrationStatus(Term term, DateOnly today)
        {
            var regPeriods = term.RegistrationPeriods.Where(rp => rp.ActiveFlag).ToList();
            if (!regPeriods.Any())
            {
                return (false, "Chưa bắt đầu");
            }

            var minStart = regPeriods.Min(rp => rp.StartDate);
            var maxEnd = regPeriods.Max(rp => rp.EndDate);

            if (today < minStart)
            {
                return (false, "Chưa bắt đầu");
            }
            else if (today >= minStart && today <= maxEnd)
            {
                return (true, "Đang mở đăng ký");
            }
            else
            {
                return (true, "Đã kết thúc");
            }
        }
    }
}

