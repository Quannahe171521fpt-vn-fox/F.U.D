using FUD.Models;
using FUD.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FUD.Repositories
{
    public class TermRepository : ITermRepository
    {
        private readonly FudContext _context;

        public TermRepository(FudContext context)
        {
            _context = context;
        }

        public async Task<List<Term>> GetAllTermsAsync()
        {
            return await _context.Terms
                .Include(t => t.PriceList)
                .Include(t => t.RegistrationPeriods)
                .OrderByDescending(t => t.StartDate)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<Term?> GetTermByIdAsync(int termId)
        {
            return await _context.Terms
                .Include(t => t.PriceList)
                .Include(t => t.RegistrationPeriods)
                .FirstOrDefaultAsync(t => t.Id == termId);
        }

        public async Task<bool> ExistsTermAsync(string name, string academicYear, int? excludeId = null)
        {
            var query = _context.Terms.AsNoTracking()
                .Where(t => t.Name.ToLower() == name.Trim().ToLower() &&
                            t.AcademicYear.ToLower() == academicYear.Trim().ToLower());

            if (excludeId.HasValue)
            {
                query = query.Where(t => t.Id != excludeId.Value);
            }

            return await query.AnyAsync();
        }

        public async Task<Term> CreateTermAsync(Term term)
        {
            _context.Terms.Add(term);
            await _context.SaveChangesAsync();
            return term;
        }

        public async Task UpdateTermAsync(Term term)
        {
            _context.Terms.Update(term);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> HasDependentDataAsync(int termId)
        {
            var hasRequests = await _context.Requests.AnyAsync(r => r.TermId == termId);
            if (hasRequests) return true;

            var hasResidencies = await _context.Residencies.AnyAsync(r => r.TermId == termId);
            if (hasResidencies) return true;

            var hasInvoices = await _context.Invoices.AnyAsync(i => i.TermId == termId);
            if (hasInvoices) return true;

            var hasBillingPeriods = await _context.BillingPeriods.AnyAsync(b => b.TermId == termId);
            if (hasBillingPeriods) return true;

            return false;
        }

        public async Task DeleteTermAsync(int termId)
        {
            // SỬA LỖI: Dùng cú pháp .ThenInclude(pl => pl!.PriceItems) thay vì toán tử điều kiện ternary '?:'
            // vì EF Core không thể biên dịch toán tử điều kiện trong Expression Tree của ThenInclude,
            // gây ngoại lệ runtime InvalidOperationException khi Manager xóa học kỳ.
            var term = await _context.Terms
                .Include(t => t.PriceList)
                    .ThenInclude(pl => pl!.PriceItems)
                .Include(t => t.RegistrationPeriods)
                .Include(t => t.UtilityPrices)
                .FirstOrDefaultAsync(t => t.Id == termId);

            if (term == null) return;

            // Xóa cascade an toàn các bản ghi cấu hình phụ thuộc (chưa có nghiệp vụ sinh viên phát sinh)
            if (term.RegistrationPeriods.Any())
            {
                _context.RegistrationPeriods.RemoveRange(term.RegistrationPeriods);
            }

            if (term.UtilityPrices.Any())
            {
                _context.UtilityPrices.RemoveRange(term.UtilityPrices);
            }

            if (term.PriceList != null)
            {
                if (term.PriceList.PriceItems.Any())
                {
                    _context.PriceItems.RemoveRange(term.PriceList.PriceItems);
                }
                _context.PriceLists.Remove(term.PriceList);
            }

            _context.Terms.Remove(term);
            await _context.SaveChangesAsync();
        }

        public async Task<List<RegistrationPeriod>> GetPeriodsByTermIdAsync(int termId)
        {
            return await _context.RegistrationPeriods
                .Where(rp => rp.TermId == termId)
                .OrderBy(rp => rp.StartDate)
                .ToListAsync();
        }

        public async Task SaveRegistrationPeriodsAsync(int termId, List<RegistrationPeriod> periods)
        {
            var existingPeriods = await _context.RegistrationPeriods
                .Where(rp => rp.TermId == termId)
                .ToListAsync();

            foreach (var incoming in periods)
            {
                var existing = existingPeriods.FirstOrDefault(p =>
                    (incoming.Id > 0 && p.Id == incoming.Id) ||
                    p.PeriodType == incoming.PeriodType);

                if (existing != null)
                {
                    existing.StartDate = incoming.StartDate;
                    existing.EndDate = incoming.EndDate;
                    existing.ActiveFlag = incoming.ActiveFlag;
                    _context.RegistrationPeriods.Update(existing);
                }
                else
                {
                    incoming.TermId = termId;
                    _context.RegistrationPeriods.Add(incoming);
                }
            }

            await _context.SaveChangesAsync();
        }

        public async Task<bool> IsPriceListPublishedAsync(int termId)
        {
            return await _context.PriceLists
                .AsNoTracking()
                .AnyAsync(pl => pl.TermId == termId && pl.Status == "Published");
        }

        public async Task TogglePeriodActiveAsync(int periodId, bool active)
        {
            var period = await _context.RegistrationPeriods.FindAsync(periodId);
            if (period != null)
            {
                if (active)
                {
                    // SỬA LỖI & BẢO VỆ RÀNG BUỘC:
                    // CSDL có chỉ mục UX_RegPeriod_ActivePerType ON Registration_Periods(TermId, PeriodType) WHERE ActiveFlag = 1.
                    // Nếu kích hoạt đợt này, ta phải tắt cờ ActiveFlag của bất kỳ đợt nào khác cùng (TermId, PeriodType)
                    // để không bị lỗi trùng lặp chỉ mục duy nhất trong SQL Server.
                    var otherActive = await _context.RegistrationPeriods
                        .Where(rp => rp.TermId == period.TermId && rp.PeriodType == period.PeriodType && rp.Id != period.Id && rp.ActiveFlag)
                        .ToListAsync();
                    foreach (var o in otherActive)
                    {
                        o.ActiveFlag = false;
                    }
                }

                period.ActiveFlag = active;
                await _context.SaveChangesAsync();
            }
        }

        public async Task<List<RegistrationPeriod>> GetOverlappingPeriodsAsync(
            string periodType, DateOnly startDate, DateOnly endDate, int excludeTermId)
        {
            // Hai khoảng [a,b] và [c,d] chồng lấn khi a <= d AND c <= b.
            return await _context.RegistrationPeriods
                .AsNoTracking()
                .Where(rp => rp.PeriodType == periodType
                          && rp.TermId != excludeTermId
                          && rp.StartDate <= endDate
                          && rp.EndDate >= startDate)
                .ToListAsync();
        }
    }
}
