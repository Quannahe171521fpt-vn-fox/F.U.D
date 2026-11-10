using System.Collections.Generic;
using System.Threading.Tasks;
using FUD.DTOs.Residencies;

namespace FUD.Services.Interfaces
{
    public interface IResidencyService
    {
        Task<List<ResidencyHistoryItemDto>> GetStudentResidencyHistoryAsync(
            int studentUserId,
            ResidencyHistoryQueryDto? query = null);

        Task<ResidencyHistoryDetailDto?> GetStudentResidencyDetailAsync(
            int studentUserId,
            int residencyId);
    }
}
