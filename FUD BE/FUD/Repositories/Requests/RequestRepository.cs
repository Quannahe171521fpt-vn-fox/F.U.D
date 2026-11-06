using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using FUD.Models;
using FUD.Repositories.Interfaces.Requests;

namespace FUD.Repositories.Requests
{
    public class RequestRepository : IRequestRepository
    {
        private readonly FudContext _context;

        public RequestRepository(FudContext context)
        {
            _context = context;
        }

        public async Task<List<Request>> GetAllRequestsAsync(string? requestType = null, string? status = null)
        {
            var query = _context.Requests
                .Include(r => r.Student).ThenInclude(s => s.User)
                .Include(r => r.Student).ThenInclude(s => s.Residencies).ThenInclude(res => res.Bed).ThenInclude(b => b.Room).ThenInclude(rm => rm.Block)
                .Include(r => r.ProcessedBy).ThenInclude(m => m.User)
                .Include(r => r.ComplaintRequest).ThenInclude(c => c!.ComplaintEvidences)
                .Include(r => r.ComplaintRequest).ThenInclude(c => c!.Violation).ThenInclude(v => v!.ViolationType)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(requestType))
            {
                query = query.Where(r => r.RequestType == requestType);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(r => r.Status == status);
            }

            return await query.OrderByDescending(r => r.CreatedTime).ToListAsync();
        }

        public async Task<List<Request>> GetStudentRequestsAsync(int studentId)
        {
            return await _context.Requests
                .Include(r => r.Student).ThenInclude(s => s.User)
                .Include(r => r.ProcessedBy).ThenInclude(m => m.User)
                .Include(r => r.ComplaintRequest).ThenInclude(c => c!.ComplaintEvidences)
                .Include(r => r.ComplaintRequest).ThenInclude(c => c!.Violation).ThenInclude(v => v!.ViolationType)
                .Where(r => r.StudentId == studentId)
                .OrderByDescending(r => r.CreatedTime)
                .ToListAsync();
        }

        public async Task<Request?> GetRequestByIdAsync(int id)
        {
            return await _context.Requests
                .Include(r => r.Student).ThenInclude(s => s.User)
                .Include(r => r.ProcessedBy).ThenInclude(m => m.User)
                .Include(r => r.ComplaintRequest).ThenInclude(c => c!.ComplaintEvidences)
                .Include(r => r.ComplaintRequest).ThenInclude(c => c!.Violation).ThenInclude(v => v!.ViolationType)
                .FirstOrDefaultAsync(r => r.Id == id);
        }

        public async Task<Request> CreateRequestAsync(Request request)
        {
            await _context.Requests.AddAsync(request);
            await _context.SaveChangesAsync();
            return request;
        }

        public async Task UpdateRequestAsync(Request request)
        {
            _context.Requests.Update(request);
            await _context.SaveChangesAsync();
        }

        public async Task<ComplaintRequest> AddComplaintRequestAsync(ComplaintRequest complaintRequest, IEnumerable<ComplaintEvidence> evidences)
        {
            await _context.ComplaintRequests.AddAsync(complaintRequest);
            if (evidences != null && evidences.Any())
            {
                await _context.ComplaintEvidences.AddRangeAsync(evidences);
            }
            await _context.SaveChangesAsync();
            return complaintRequest;
        }

        public async Task<Term?> GetCurrentActiveTermAsync()
        {
            return await _context.Terms
                .OrderByDescending(t => t.StartDate)
                .FirstOrDefaultAsync();
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
    }
}

