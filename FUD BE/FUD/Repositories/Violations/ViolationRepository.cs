using FUD.DTOs.Violations;
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
                .Where(r => r.Bed != null && r.Bed.RoomId == roomId && (r.Status == "Active" || r.Status == "Reserved" || r.Status == "CheckedIn"))
                .ToListAsync();
        }

        public async Task<Student?> GetStudentByCodeAsync(string studentCode)
        {
            var codeLower = studentCode.Trim().ToLower();
            return await _context.Students
                .AsNoTracking()
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => (s.User != null && s.User.Code != null && s.User.Code.ToLower() == codeLower)
                                       || (s.User != null && s.User.Email != null && s.User.Email.ToLower().StartsWith(codeLower + "@"))
                                       || (s.User != null && s.User.Code != null && s.User.Code.ToLower().Contains(codeLower))
                                       || (s.User != null && s.User.Email != null && s.User.Email.ToLower().Contains(codeLower)));
        }

        public async Task<Residency?> GetCurrentResidencyByStudentIdAsync(int studentId)
        {
            return await _context.Residencies
                .AsNoTracking()
                .Include(r => r.Bed)
                    .ThenInclude(b => b.Room)
                        .ThenInclude(rm => rm.Block)
                .Include(r => r.Term)
                .FirstOrDefaultAsync(r => r.StudentId == studentId && (r.Status == "Active" || r.Status == "Reserved" || r.Status == "CheckedIn"));
        }

        public async Task<bool> RoomExistsAsync(int roomId)
        {
            return await _context.Rooms.AnyAsync(r => r.Id == roomId);
        }

        public async Task<string> GenerateViolationCodeAsync()
        {
            var today = DateTime.UtcNow;
            var datePrefix = $"VIO-{today:yyyyMMdd}-";
            
            var count = await _context.Violations
                .CountAsync(v => v.Code.StartsWith(datePrefix));

            return $"{datePrefix}{(count + 1):D4}";
        }

        public async Task AddViolationsAsync(IEnumerable<Violation> violations)
        {
            await _context.Violations.AddRangeAsync(violations);
            await _context.SaveChangesAsync();
        }

        public async Task<List<Violation>> GetViolationsAsync(int? roomId = null, int? studentId = null, string? status = null)
        {
            var query = _context.Violations
                .AsNoTracking()
                .Include(v => v.ViolationType)
                .Include(v => v.Student).ThenInclude(s => s.User)
                .Include(v => v.Room).ThenInclude(r => r.Block)
                .Include(v => v.RecordedBy).ThenInclude(g => g.User)
                .Include(v => v.EvaluatedBy).ThenInclude(m => m.User)
                .Include(v => v.Residency).ThenInclude(res => res!.Term)
                .Include(v => v.ViolationEvidences)
                .Include(v => v.ComplaintRequests)
                .AsQueryable();

            if (roomId.HasValue)
            {
                query = query.Where(v => v.RoomId == roomId.Value);
            }

            if (studentId.HasValue)
            {
                query = query.Where(v => v.StudentId == studentId.Value);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(v => v.Status == status);
            }

            return await query.OrderByDescending(v => v.OccurrenceTime).ToListAsync();
        }

        public async Task<Violation?> GetViolationByIdAsync(int id)
        {
            return await _context.Violations
                .Include(v => v.ViolationType)
                .Include(v => v.Student).ThenInclude(s => s.User)
                .Include(v => v.Room).ThenInclude(r => r.Block)
                .Include(v => v.RecordedBy).ThenInclude(g => g.User)
                .Include(v => v.EvaluatedBy).ThenInclude(m => m.User)
                .Include(v => v.Residency).ThenInclude(res => res!.Term)
                .Include(v => v.ViolationEvidences)
                .Include(v => v.ComplaintRequests)
                .FirstOrDefaultAsync(v => v.Id == id);
        }

        public async Task<Violation?> GetViolationByCodeAsync(string code)
        {
            return await _context.Violations
                .Include(v => v.ViolationType)
                .Include(v => v.Student).ThenInclude(s => s.User)
                .Include(v => v.Room).ThenInclude(r => r.Block)
                .Include(v => v.RecordedBy).ThenInclude(g => g.User)
                .Include(v => v.EvaluatedBy).ThenInclude(m => m.User)
                .Include(v => v.Residency).ThenInclude(res => res!.Term)
                .Include(v => v.ViolationEvidences)
                .Include(v => v.ComplaintRequests)
                .FirstOrDefaultAsync(v => v.Code == code);
        }

        public async Task UpdateViolationAsync(Violation violation)
        {
            // Dùng ExecuteUpdateAsync để tránh xung đột EF tracking khi entity đến từ AsNoTracking
            await _context.Violations
                .Where(v => v.Id == violation.Id)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(v => v.Status, violation.Status)
                    .SetProperty(v => v.FinalLevel, violation.FinalLevel)
                    .SetProperty(v => v.FinalizedTime, violation.FinalizedTime)
                    .SetProperty(v => v.InitialLevel, violation.InitialLevel)
                    .SetProperty(v => v.RequestDeadline, violation.RequestDeadline)
                    .SetProperty(v => v.EvaluatedById, violation.EvaluatedById)
                    .SetProperty(v => v.ManagerNote, violation.ManagerNote)
                );
        }

        public async Task<User?> GetUserByIdAsync(int userId)
        {
            return await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        }

        public async Task UpdateUserAsync(User user)
        {
            _context.Users.Update(user);
            await _context.SaveChangesAsync();
        }

        public async Task<int> GetStudentPriorViolationsCountAsync(int studentId, int currentViolationId)
        {
            return await _context.Violations
                .CountAsync(v => v.StudentId == studentId && v.Id != currentViolationId && v.Status == "Finalized");
        }

        public async Task<List<BlockForViolationDto>> GetBlocksByGuardDormAsync(int guardUserId)
        {
            var guard = await _context.Guards
                .Include(g => g.Dorm)
                    .ThenInclude(d => d.Blocks)
                .FirstOrDefaultAsync(g => g.UserId == guardUserId);

            if (guard == null) return new List<BlockForViolationDto>();

            return guard.Dorm.Blocks
                .Where(b => b.Status == "Active")
                .OrderBy(b => b.Name)
                .Select(b => new BlockForViolationDto
                {
                    Id = b.Id,
                    Name = b.Name,
                    Code = b.Code,
                    DormId = guard.DormId,
                    DormName = guard.Dorm.Name
                }).ToList();
        }
    }
}
