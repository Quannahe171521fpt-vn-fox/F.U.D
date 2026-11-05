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
    }
}
