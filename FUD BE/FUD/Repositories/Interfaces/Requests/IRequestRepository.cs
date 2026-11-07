using System.Collections.Generic;
using System.Threading.Tasks;
using FUD.Models;

namespace FUD.Repositories.Interfaces.Requests
{
    public interface IRequestRepository
    {
        Task<List<Request>> GetAllRequestsAsync(string? requestType = null, string? status = null);
        Task<List<Request>> GetStudentRequestsAsync(int studentId);
        Task<Request?> GetRequestByIdAsync(int id);
        Task<Request> CreateRequestAsync(Request request);
        Task UpdateRequestAsync(Request request);
        Task<ComplaintRequest> AddComplaintRequestAsync(ComplaintRequest complaintRequest, IEnumerable<ComplaintEvidence> evidences);
        Task<Term?> GetCurrentActiveTermAsync();
        Task<User?> GetUserByIdAsync(int userId);
        Task UpdateUserAsync(User user);
    }
}

