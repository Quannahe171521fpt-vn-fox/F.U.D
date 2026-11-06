using FUD.DTOs.Violations;

namespace FUD.Services.Interfaces.Violations
{
    public interface IViolationService
    {
        Task<List<ViolationTypeDto>> GetViolationTypesAsync();
        Task<List<RoomStudentDto>> GetStudentsByRoomAsync(int roomId);
        Task<RoomStudentDto?> SearchStudentByCodeAsync(string studentCode, int? currentRoomId = null);
        Task<List<ViolationItemDto>> RecordViolationsAsync(CreateViolationRequestDto request, int guardUserId);
        Task<List<ViolationItemDto>> GetViolationsAsync(int? roomId = null, int? studentId = null, string? status = null);
        Task<ViolationItemDto?> GetViolationByIdAsync(int id);
        
        // UC19 & UC21
        Task<List<ViolationItemDto>> GetStudentNoticesAsync(int studentUserId);
        Task<List<ViolationItemDto>> GetStudentHistoryAsync(int studentUserId, string? term = null, string? status = null);

        // UC23
        Task<ViolationItemDto> EvaluateViolationAsync(int violationId, int managerUserId, EvaluateViolationRequestDto request);
        Task UnsuspendStudentAsync(int studentId, int managerUserId, string? note = null);
    }
}
