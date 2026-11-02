using FUD.DTOs.Pricing;

namespace FUD.Services.Interfaces
{
    public interface ITermService
    {
        Task<List<TermSummaryDto>> GetAllTermsAsync();
        Task<TermDetailDto> GetTermDetailAsync(int termId);
        Task<TermDetailDto> CreateTermAsync(CreateTermRequestDto dto);
        Task<TermDetailDto> UpdateTermAsync(int termId, UpdateTermRequestDto dto);
        Task DeleteTermAsync(int termId);
        Task<TermDetailDto> SaveRegistrationScheduleAsync(int termId, SaveRegistrationScheduleRequestDto dto);
        Task TogglePeriodActiveAsync(int termId, int periodId, bool active);
    }
}
