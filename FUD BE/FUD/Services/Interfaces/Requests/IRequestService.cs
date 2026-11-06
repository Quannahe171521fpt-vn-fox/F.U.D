using System.Collections.Generic;
using System.Threading.Tasks;
using FUD.DTOs.Requests;

namespace FUD.Services.Interfaces.Requests
{
    public interface IRequestService
    {
        Task<List<RequestItemDto>> GetAllRequestsAsync(string? requestType = null, string? status = null);
        Task<List<RequestItemDto>> GetStudentRequestsAsync(int studentId);
        Task<RequestItemDto> CreateRequestAsync(int studentId, CreateRequestDto dto);
        Task<RequestItemDto> ProcessRequestAsync(int requestId, int managerId, ProcessRequestDto dto);
    }
}
