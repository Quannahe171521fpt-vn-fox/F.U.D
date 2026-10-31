using FUD.DTOs.Pricing;

namespace FUD.Services.Interfaces
{
    public interface IPricingService
    {
        Task<List<TermLookupDto>> GetTermsLookupAsync();
        Task<TermPricingResponseDto> GetPricingByTermAsync(int termId);
        Task<TermPricingResponseDto> SavePricingAsync(int managerUserId, SavePricingRequestDto request);
    }
}

