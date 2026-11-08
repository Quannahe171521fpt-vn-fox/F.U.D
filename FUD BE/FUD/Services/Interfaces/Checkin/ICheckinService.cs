using System.Collections.Generic;
using System.Threading.Tasks;
using FUD.DTOs.Checkin;

namespace FUD.Services.Interfaces.Checkin
{
    public interface ICheckinService
    {
        Task<List<PendingCheckinStudentDto>> GetPendingStudentsAsync(int? blockId = null, int? roomId = null, string? search = null);
        Task<List<RoomEquipmentCheckinDto>> GetRoomEquipmentsAsync(int roomId, int? bedId = null);
        Task<CheckinHandoverResponseDto> CompleteCheckinAsync(int guardUserId, CreateCheckinHandoverDto dto);
        Task<List<CheckinHandoverResponseDto>> GetCheckinHistoryAsync(int? blockId = null, int? roomId = null, string? search = null);
        Task<CheckinHandoverResponseDto?> GetCheckinByIdAsync(int id);
    }
}
