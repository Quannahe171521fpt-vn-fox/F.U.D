using FUD.Models;

namespace FUD.Repositories.Interfaces.Violations
{
    public interface IViolationRepository
    {
        Task<List<ViolationType>> GetViolationTypesAsync();
        Task<List<Residency>> GetActiveResidenciesByRoomIdAsync(int roomId);
        Task<Student?> GetStudentByCodeAsync(string studentCode);
        Task<Residency?> GetCurrentResidencyByStudentIdAsync(int studentId);
        Task<bool> RoomExistsAsync(int roomId);
        Task<string> GenerateViolationCodeAsync();
        Task AddViolationsAsync(IEnumerable<Violation> violations);
        Task<List<Violation>> GetViolationsAsync(int? roomId = null, int? studentId = null);
        Task<List<Violation>> FindDuplicateViolationsAsync(List<int> studentIds, int violationTypeId, DateTime occurrenceTime);
    }
}
