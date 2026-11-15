using FUD.Models;
using FUD.Repositories.Interfaces;
using FUD.Repositories.Interfaces.Keeping;
using Microsoft.EntityFrameworkCore;

namespace FUD.Repositories.Keeping
{
    /// <summary>Repository cho module Giữ phòng (UC02). Tất cả truy vấn đều dùng index có sẵn,
    /// không include thừa để tránh N+1 khi ~200 sinh viên gọi đồng thời.</summary>
    public class KeepingRepository : IKeepingRepository
    {
        private readonly FudContext _context;

        public KeepingRepository(FudContext context)
        {
            _context = context;
        }

        public async Task<Residency?> GetActiveResidencyAsync(int studentId)
        {
            return await _context.Residencies
                .AsNoTracking()
                .Include(r => r.Bed)
                    .ThenInclude(b => b.Room)
                        .ThenInclude(rm => rm.Block)
                            .ThenInclude(bl => bl.Dorm)
                .Include(r => r.Term)
                .FirstOrDefaultAsync(r => r.StudentId == studentId && r.Status == "Active");
        }

        public async Task<(RegistrationPeriod? Period, Term? NextTerm, Term? CurrentTerm)>
            GetOpenKeepingPeriodAsync(int studentId, DateOnly today)
        {
            // Kỳ hiện tại: kỳ mà sinh viên đang có cư trú Active.
            var currentTerm = await _context.Residencies
                .AsNoTracking()
                .Where(r => r.StudentId == studentId && r.Status == "Active")
                .Select(r => r.Term)
                .FirstOrDefaultAsync();

            if (currentTerm == null)
            {
                return (null, null, null);
            }

            // Kỳ kế tiếp: lấy từ Registration_Period loại Keeping đang Active,
            // sau đó kiểm tra Term của kỳ đó nằm sau kỳ hiện tại (GB-04).
            var nextTerm = await _context.RegistrationPeriods
                .AsNoTracking()
                .Where(p => p.PeriodType == "Keeping" && p.ActiveFlag)
                .Where(p => p.StartDate <= today && p.EndDate >= today)
                .Select(p => p.Term)
                .Where(t => t.StartDate > currentTerm.EndDate)
                .OrderBy(t => t.StartDate)
                .FirstOrDefaultAsync();

            if (nextTerm == null)
            {
                return (null, null, currentTerm);
            }

            var period = await _context.RegistrationPeriods
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.TermId == nextTerm.Id
                                        && p.PeriodType == "Keeping"
                                        && p.ActiveFlag);

            return (period, nextTerm, currentTerm);
        }

        public async Task<bool> IsPriceListPublishedAsync(int termId)
        {
            return await _context.PriceLists
                .AsNoTracking()
                .AnyAsync(pl => pl.TermId == termId && pl.Status == "Published");
        }

        public async Task<bool> HasOverdueInvoiceAsync(int studentId)
        {
            return await _context.Invoices
                .AsNoTracking()
                .AnyAsync(i => i.StudentId == studentId && i.Status == "Overdue");
        }

        public async Task<bool> HasUnpaidInvoiceAsync(int studentId)
        {
            return await _context.Invoices
                .AsNoTracking()
                .AnyAsync(i => i.StudentId == studentId && i.Status == "Unpaid");
        }

        public async Task<bool> HasCompletedKeepingRequestAsync(int studentId, int termId)
        {
            return await _context.Requests
                .AsNoTracking()
                .AnyAsync(r => r.StudentId == studentId
                            && r.TermId == termId
                            && r.RequestType == "Keeping"
                            && r.Status == "Completed");
        }

        public async Task<Request?> GetCompletedKeepingRequestAsync(int studentId, int termId)
        {
            return await _context.Requests
                .AsNoTracking()
                .Include(r => r.Term)
                .FirstOrDefaultAsync(r => r.StudentId == studentId
                                       && r.TermId == termId
                                       && r.RequestType == "Keeping"
                                       && r.Status == "Completed");
        }

        public async Task<Request?> GetKeepingRequestByIdAsync(int requestId)
        {
            return await _context.Requests
                .AsNoTracking()
                .Include(r => r.Term)
                .Include(r => r.Student)
                .FirstOrDefaultAsync(r => r.Id == requestId && r.RequestType == "Keeping");
        }

        public async Task<Residency?> GetResidencyByStudentAndTermAsync(int studentId, int termId)
        {
            return await _context.Residencies
                .AsNoTracking()
                .Include(r => r.Bed)
                    .ThenInclude(b => b.Room)
                        .ThenInclude(rm => rm.Block)
                            .ThenInclude(bl => bl.Dorm)
                .FirstOrDefaultAsync(r => r.StudentId == studentId && r.TermId == termId);
        }

        public async Task<decimal?> GetKeepingBedPriceAsync(int bedId, int termId)
        {
            var bed = await _context.Beds
                .AsNoTracking()
                .Include(b => b.Room)
                    .ThenInclude(r => r.Block)
                .FirstOrDefaultAsync(b => b.Id == bedId);

            if (bed == null) return null;

            var dormId = bed.Room.Block.DormId;
            var roomTypeId = bed.Room.RoomTypeId;

            var price = await _context.PriceItems
                .AsNoTracking()
                .Where(pi => pi.PriceList.TermId == termId && pi.DormId == dormId && pi.RoomTypeId == roomTypeId)
                .Select(pi => (decimal?)pi.UnitPrice)
                .FirstOrDefaultAsync();

            return price;
        }

        public async Task<int> GetKeepingMonthsAsync(int bedId, int termId)
        {
            return await Task.FromResult(1);
        }

        public async Task<bool> IsBedOccupiedForTermAsync(int bedId, int termId)
        {
            // 'Upcoming' cũng bị tính là chiếm chỗ để chống double-book cho kỳ kế tiếp.
            return await _context.Residencies
                .AsNoTracking()
                .AnyAsync(r => r.BedId == bedId
                            && r.TermId == termId
                            && (r.Status == "Active" || r.Status == "Reserved" || r.Status == "Upcoming"));
        }

        public async Task<bool> HasResidencyForTermAsync(int studentId, int termId)
        {
            return await _context.Residencies
                .AsNoTracking()
                .AnyAsync(r => r.StudentId == studentId
                            && r.TermId == termId
                            && (r.Status == "Active" || r.Status == "Reserved" || r.Status == "Upcoming"));
        }

        public async Task<List<Residency>> GetActiveResidenciesForTermAsync(int termId)
        {
            return await _context.Residencies
                .Include(r => r.Bed)
                .Where(r => r.TermId == termId && r.Status == "Active")
                .ToListAsync();
        }

        public async Task<HashSet<int>> GetStudentsWithCompletedKeepingAsync(int termId)
        {
            var ids = await _context.Requests
                .AsNoTracking()
                .Where(r => r.TermId == termId
                            && r.RequestType == "Keeping"
                            && r.Status == "Completed")
                .Select(r => r.StudentId)
                .ToListAsync();

            return new HashSet<int>(ids);
        }

        public async Task<IAccountTransaction> BeginTransactionAsync()
        {
            // EF Core InMemory (dùng cho unit test) không hỗ trợ transaction.
            // Trong trường hợp đó trả về no-op transaction để Service Layer không cần biết.
            // Kiểm tra bằng ProviderName để không cần thêm package InMemory vào project chính.
            var providerName = _context.Database.ProviderName;
            if (providerName != null && providerName.Contains("InMemory", StringComparison.OrdinalIgnoreCase))
            {
                return new NoOpTransaction();
            }

            var tx = await _context.Database.BeginTransactionAsync();
            return new KeepingTransaction(tx);
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }

        /// <summary>Transaction no-op cho provider không hỗ trợ (ví dụ EF Core InMemory).</summary>
        private sealed class NoOpTransaction : IAccountTransaction
        {
            public Task CommitAsync() => Task.CompletedTask;

            public Task RollbackAsync() => Task.CompletedTask;

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }

        /// <summary>Wrapper cho IDbContextTransaction, giữ Service Layer không phụ thuộc EF Core.</summary>
        private sealed class KeepingTransaction : IAccountTransaction
        {
            private readonly Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction _transaction;
            private bool _disposed;

            public KeepingTransaction(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction)
            {
                _transaction = transaction;
            }

            public async Task CommitAsync()
            {
                await _transaction.CommitAsync();
            }

            public async Task RollbackAsync()
            {
                await _transaction.RollbackAsync();
            }

            public async ValueTask DisposeAsync()
            {
                if (!_disposed)
                {
                    _disposed = true;
                    await _transaction.DisposeAsync();
                }
            }
        }
    }
}
