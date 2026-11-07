using FUD.Models;

namespace FUD.Repositories.Interfaces
{
    public interface ITermRepository
    {
        Task<List<Term>> GetAllTermsAsync();
        Task<Term?> GetTermByIdAsync(int termId);
        Task<bool> ExistsTermAsync(string name, string academicYear, int? excludeId = null);
        Task<Term> CreateTermAsync(Term term);
        Task UpdateTermAsync(Term term);
        Task<bool> HasDependentDataAsync(int termId);
        Task DeleteTermAsync(int termId);
        Task<List<RegistrationPeriod>> GetPeriodsByTermIdAsync(int termId);
        Task SaveRegistrationPeriodsAsync(int termId, List<RegistrationPeriod> periods);
        Task<bool> IsPriceListPublishedAsync(int termId);
        Task TogglePeriodActiveAsync(int periodId, bool active);

        /// <summary>
        /// Tìm các kỳ đăng ký (Registration_Period) cùng loại ở các học kỳ khác có khoảng thời gian
        /// chồng lấn với khoảng thời gian đang xét. Dùng để chặn chồng kỳ cùng loại (GB-04).
        /// </summary>
        Task<List<RegistrationPeriod>> GetOverlappingPeriodsAsync(string periodType, DateOnly startDate, DateOnly endDate, int excludeTermId);
    }
}
