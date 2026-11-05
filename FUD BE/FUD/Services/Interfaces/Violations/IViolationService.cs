using FUD.DTOs.Violations;

namespace FUD.Services.Interfaces.Violations
{
    public interface IViolationService
    {
        Task<List<ViolationTypeDto>> GetViolationTypesAsync();
        Task<List<RoomStudentDto>> GetStudentsByRoomAsync(int roomId);
        Task<RoomStudentDto?> SearchStudentByCodeAsync(string studentCode, int? currentRoomId = null);
        Task<DuplicateViolationCheckResultDto> CheckDuplicateAsync(List<int> studentIds, int violationTypeId, DateTime occurrenceTime);
        Task<List<ViolationItemDto>> RecordViolationsAsync(CreateViolationRequestDto request, int guardUserId);
        Task<List<ViolationItemDto>> GetViolationsAsync(int? roomId = null, int? studentId = null);
    }
}
