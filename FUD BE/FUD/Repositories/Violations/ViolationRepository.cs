using FUD.Models;
using FUD.Repositories.Interfaces.Violations;
using Microsoft.EntityFrameworkCore;

namespace FUD.Repositories.Violations
{
    public class ViolationRepository : IViolationRepository
    {
        private readonly FudContext _context;

        public ViolationRepository(FudContext context)
        {
            _context = context;
        }

        public async Task<List<ViolationType>> GetViolationTypesAsync()
        {
            return await _context.ViolationTypes
                .AsNoTracking()
                .OrderBy(vt => vt.Id)
                .ToListAsync();
        }

        public async Task<List<Residency>> GetActiveResidenciesByRoomIdAsync(int roomId)
        {
            return await _context.Residencies
                .AsNoTracking()
                .Include(r => r.Student)
                    .ThenInclude(s => s.User)
                .Include(r => r.Bed)
                .Where(r => r.Bed.RoomId == roomId && r.Status == "Active")
                .ToListAsync();
        }

        public async Task<Student?> GetStudentByCodeAsync(string studentCode)
        {
            return await _context.Students
                .AsNoTracking()
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.User.Code == studentCode || s.User.Email.StartsWith(studentCode + "@"));
        }

        public async Task<Residency?> GetCurrentResidencyByStudentIdAsync(int studentId)
        {
            return await _context.Residencies
                .AsNoTracking()
                .Include(r => r.Bed)
                    .ThenInclude(b => b.Room)
                        .ThenInclude(rm => rm.Block)
                .FirstOrDefaultAsync(r => r.StudentId == studentId && r.Status == "Active");
        }

        public async Task<bool> RoomExistsAsync(int roomId)
        {
            return await _context.Rooms.AnyAsync(r => r.Id == roomId);
        }

        public async Task<string> GenerateViolationCodeAsync()
        {
            var today = DateTime.UtcNow;
            var datePrefix = $"VIO-{today:yyyyMMdd}-";
            
            // Get count of violations created today
            var count = await _context.Violations
                .CountAsync(v => v.Code.StartsWith(datePrefix));

            return $"{datePrefix}{(count + 1):D4}";
        }

        public async Task AddViolationsAsync(IEnumerable<Violation> violations)
        {
            await _context.Violations.AddRangeAsync(violations);
            await _context.SaveChangesAsync();
        }

        public async Task<List<Violation>> GetViolationsAsync(int? roomId = null, int? studentId = null)
        {
            var query = _context.Violations
                .AsNoTracking()
                .Include(v => v.ViolationType)
                .Include(v => v.Student).ThenInclude(s => s.User)
                .Include(v => v.Room).ThenInclude(r => r.Block)
                .Include(v => v.RecordedBy).ThenInclude(g => g.User)
                .Include(v => v.ViolationEvidences)
                .AsQueryable();

            if (roomId.HasValue)
            {
                query = query.Where(v => v.RoomId == roomId.Value);
            }

            if (studentId.HasValue)
            {
                query = query.Where(v => v.StudentId == studentId.Value);
            }

            return await query.OrderByDescending(v => v.OccurrenceTime).ToListAsync();
        }

        public async Task<List<Violation>> FindDuplicateViolationsAsync(List<int> studentIds, int violationTypeId, DateTime occurrenceTime)
        {
            // Trùng lặp: cùng sinh viên, cùng loại vi phạm và cùng ngày (hoặc trong khoảng +- 12 giờ để không bị lệch múi giờ UTC/Local)
            // Lấy bắt đầu và kết thúc của ngày xảy ra vi phạm
            var dateOnly = occurrenceTime.Date;
            var fromTime = dateOnly.AddDays(-1);
            var toTime = dateOnly.AddDays(2);

            return await _context.Violations
                .AsNoTracking()
                .Include(v => v.ViolationType)
                .Include(v => v.Student).ThenInclude(s => s.User)
                .Include(v => v.Room).ThenInclude(r => r.Block)
                .Include(v => v.RecordedBy).ThenInclude(g => g.User)
                .Include(v => v.ViolationEvidences)
                .Where(v => studentIds.Contains(v.StudentId)
                            && v.ViolationTypeId == violationTypeId
                            && (v.OccurrenceTime.Date == occurrenceTime.Date
                                || (v.OccurrenceTime >= fromTime && v.OccurrenceTime <= toTime)))
                .ToListAsync();
        }
    }
}
