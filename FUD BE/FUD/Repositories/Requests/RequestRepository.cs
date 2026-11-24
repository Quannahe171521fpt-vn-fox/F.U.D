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
                .AsSplitQuery()
                .Include(r => r.Term).ThenInclude(t => t.UtilityPrices)
                .Include(r => r.Student).ThenInclude(s => s.User)
                .Include(r => r.Student).ThenInclude(s => s.Residencies).ThenInclude(res => res.Bed).ThenInclude(b => b.Room).ThenInclude(rm => rm.Block)
                .Include(r => r.ProcessedBy).ThenInclude(m => m.User)
                .Include(r => r.ComplaintRequest).ThenInclude(c => c!.ComplaintEvidences)
                .Include(r => r.ComplaintRequest).ThenInclude(c => c!.Violation).ThenInclude(v => v!.ViolationType)
                .Include(r => r.BedTransferRequest).ThenInclude(bt => bt!.FromResidency)
                .Include(r => r.BedTransferRequest).ThenInclude(bt => bt!.FromBed).ThenInclude(b => b.Room).ThenInclude(rm => rm.Block).ThenInclude(bl => bl.Dorm)
                .Include(r => r.BedTransferRequest).ThenInclude(bt => bt!.FromBed).ThenInclude(b => b.Room).ThenInclude(rm => rm.Beds).ThenInclude(bd => bd.Residencies)
                .Include(r => r.BedTransferRequest).ThenInclude(bt => bt!.ToBed).ThenInclude(b => b.Room).ThenInclude(rm => rm.RoomType)
                .Include(r => r.BedTransferRequest).ThenInclude(bt => bt!.ToBed).ThenInclude(b => b.Room).ThenInclude(rm => rm.Block).ThenInclude(bl => bl.Dorm)
                .Include(r => r.BedTransferRequest).ThenInclude(bt => bt!.AssignedGuard).ThenInclude(g => g!.User)
                .Include(r => r.BedTransferRequest).ThenInclude(bt => bt!.MeterReadings)
                .Include(r => r.BedTransferRequest).ThenInclude(bt => bt!.Invoices)
                .Include(r => r.Handover).ThenInclude(h => h!.Guard).ThenInclude(g => g.User)
                .Include(r => r.Handover).ThenInclude(h => h!.HandoverItems).ThenInclude(hi => hi.Equipment).ThenInclude(eq => eq.Catalog)
                .Include(r => r.Handover).ThenInclude(h => h!.Invoices)
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
                .AsSplitQuery()
                .Include(r => r.Term).ThenInclude(t => t.UtilityPrices)
                .Include(r => r.Student).ThenInclude(s => s.User)
                .Include(r => r.ProcessedBy).ThenInclude(m => m.User)
                .Include(r => r.ComplaintRequest).ThenInclude(c => c!.ComplaintEvidences)
                .Include(r => r.ComplaintRequest).ThenInclude(c => c!.Violation).ThenInclude(v => v!.ViolationType)
                .Include(r => r.BedTransferRequest).ThenInclude(bt => bt!.FromResidency)
                .Include(r => r.BedTransferRequest).ThenInclude(bt => bt!.FromBed).ThenInclude(b => b.Room).ThenInclude(rm => rm.Block).ThenInclude(bl => bl.Dorm)
                .Include(r => r.BedTransferRequest).ThenInclude(bt => bt!.FromBed).ThenInclude(b => b.Room).ThenInclude(rm => rm.Beds).ThenInclude(bd => bd.Residencies)
                .Include(r => r.BedTransferRequest).ThenInclude(bt => bt!.ToBed).ThenInclude(b => b.Room).ThenInclude(rm => rm.RoomType)
                .Include(r => r.BedTransferRequest).ThenInclude(bt => bt!.ToBed).ThenInclude(b => b.Room).ThenInclude(rm => rm.Block).ThenInclude(bl => bl.Dorm)
                .Include(r => r.BedTransferRequest).ThenInclude(bt => bt!.AssignedGuard).ThenInclude(g => g!.User)
                .Include(r => r.BedTransferRequest).ThenInclude(bt => bt!.MeterReadings)
                .Include(r => r.BedTransferRequest).ThenInclude(bt => bt!.Invoices)
                .Include(r => r.Handover).ThenInclude(h => h!.Guard).ThenInclude(g => g.User)
                .Include(r => r.Handover).ThenInclude(h => h!.HandoverItems).ThenInclude(hi => hi.Equipment).ThenInclude(eq => eq.Catalog)
                .Include(r => r.Handover).ThenInclude(h => h!.Invoices)
                .Where(r => r.StudentId == studentId)
                .OrderByDescending(r => r.CreatedTime)
                .ToListAsync();
        }

        public async Task<Request?> GetRequestByIdAsync(int id)
        {
            return await _context.Requests
                .AsSplitQuery()
                .Include(r => r.Term).ThenInclude(t => t.UtilityPrices)
                .Include(r => r.Student).ThenInclude(s => s.User)
                .Include(r => r.ProcessedBy).ThenInclude(m => m.User)
                .Include(r => r.ComplaintRequest).ThenInclude(c => c!.ComplaintEvidences)
                .Include(r => r.ComplaintRequest).ThenInclude(c => c!.Violation).ThenInclude(v => v!.ViolationType)
                .Include(r => r.BedTransferRequest).ThenInclude(bt => bt!.FromResidency)
                .Include(r => r.BedTransferRequest).ThenInclude(bt => bt!.FromBed).ThenInclude(b => b.Room).ThenInclude(rm => rm.Block).ThenInclude(bl => bl.Dorm)
                .Include(r => r.BedTransferRequest).ThenInclude(bt => bt!.FromBed).ThenInclude(b => b.Room).ThenInclude(rm => rm.Beds).ThenInclude(bd => bd.Residencies)
                .Include(r => r.BedTransferRequest).ThenInclude(bt => bt!.ToBed).ThenInclude(b => b.Room).ThenInclude(rm => rm.RoomType)
                .Include(r => r.BedTransferRequest).ThenInclude(bt => bt!.ToBed).ThenInclude(b => b.Room).ThenInclude(rm => rm.Block).ThenInclude(bl => bl.Dorm)
                .Include(r => r.BedTransferRequest).ThenInclude(bt => bt!.AssignedGuard).ThenInclude(g => g!.User)
                .Include(r => r.BedTransferRequest).ThenInclude(bt => bt!.MeterReadings)
                .Include(r => r.BedTransferRequest).ThenInclude(bt => bt!.Invoices)
                .Include(r => r.Handover).ThenInclude(h => h!.Guard).ThenInclude(g => g.User)
                .Include(r => r.Handover).ThenInclude(h => h!.HandoverItems).ThenInclude(hi => hi.Equipment).ThenInclude(eq => eq.Catalog)
                .Include(r => r.Handover).ThenInclude(h => h!.Invoices)
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

        public async Task<Residency?> GetStudentActiveResidencyAsync(int studentId)
        {
            return await _context.Residencies
                .AsNoTracking()
                .Include(r => r.Term)
                .Include(r => r.Bed).ThenInclude(b => b.Room).ThenInclude(rm => rm.RoomType)
                .Include(r => r.Bed).ThenInclude(b => b.Room).ThenInclude(rm => rm.Block).ThenInclude(bl => bl.Dorm)
                .Where(r => r.StudentId == studentId && r.Status == "Active")
                .OrderByDescending(r => r.Term.StartDate)
                .FirstOrDefaultAsync();
        }

        public async Task<bool> HasOverdueInvoicesAsync(int studentId)
        {
            return await _context.Invoices
                .AsNoTracking()
                .AnyAsync(i => i.StudentId == studentId && i.Status == "Overdue");
        }

        public async Task<bool> HasPendingBedTransferAsync(int studentId, int termId)
        {
            return await _context.Requests
                .AsNoTracking()
                .AnyAsync(r => r.StudentId == studentId
                            && r.TermId == termId
                            && r.RequestType == "BedTransfer"
                            && (r.Status == "Pending" || r.Status == "Approved"));
        }

        public async Task<List<Bed>> GetAvailableBedsForTransferAsync(string studentGender, int currentBedId, int termId)
        {
            return await _context.Beds
                .AsNoTracking()
                .Include(b => b.Room).ThenInclude(rm => rm.RoomType)
                .Include(b => b.Room).ThenInclude(rm => rm.Block).ThenInclude(bl => bl.Dorm)
                .Where(b => b.Id != currentBedId
                         && b.Status == "Free"
                         && b.Room.Status == "Active"
                         && b.Room.Block.Status == "Active"
                         && b.Room.Block.Dorm.Status == "Active"
                         && b.Room.Block.GenderType == studentGender
                         && !_context.Residencies.Any(res => res.BedId == b.Id
                                                          && res.TermId == termId
                                                          && (res.Status == "Reserved" || res.Status == "Active"))
                         && !_context.BedTransferRequests.Any(bt => bt.ToBedId == b.Id
                                                                 && bt.Request.TermId == termId
                                                                 && (bt.Request.Status == "Pending" || bt.Request.Status == "Approved")))
                .OrderBy(b => b.Room.Block.Dorm.Code)
                .ThenBy(b => b.Room.Block.Code)
                .ThenBy(b => b.Room.RoomNumber)
                .ThenBy(b => b.BedCode)
                .ToListAsync();
        }

        public async Task<Bed?> GetBedWithLocationByIdAsync(int bedId)
        {
            return await _context.Beds
                .AsNoTracking()
                .Include(b => b.Room).ThenInclude(rm => rm.RoomType)
                .Include(b => b.Room).ThenInclude(rm => rm.Block).ThenInclude(bl => bl.Dorm)
                .FirstOrDefaultAsync(b => b.Id == bedId);
        }

        public async Task<bool> IsBedAvailableForTransferAsync(int bedId, int termId)
        {
            var hasResidency = await _context.Residencies
                .AsNoTracking()
                .AnyAsync(res => res.BedId == bedId
                              && res.TermId == termId
                              && (res.Status == "Reserved" || res.Status == "Active"));

            if (hasResidency) return false;

            var hasPendingTransfer = await _context.BedTransferRequests
                .AsNoTracking()
                .AnyAsync(bt => bt.ToBedId == bedId
                             && bt.Request.TermId == termId
                             && (bt.Request.Status == "Pending" || bt.Request.Status == "Approved"));

            return !hasPendingTransfer;
        }

        public async Task<Dictionary<(int DormId, int RoomTypeId), decimal>> GetPublishedRoomPricesAsync(int termId)
        {
            var priceItems = await _context.PriceItems
                .AsNoTracking()
                .Where(pi => pi.PriceList.TermId == termId && pi.PriceList.Status == "Published")
                .Select(pi => new { pi.DormId, pi.RoomTypeId, pi.UnitPrice })
                .ToListAsync();

            return priceItems.ToDictionary(x => (x.DormId, x.RoomTypeId), x => x.UnitPrice);
        }

        public async Task<Request> CreateBedTransferRequestAsync(Request request, BedTransferRequest transferRequest, RequestLog log)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var toBed = await _context.Beds.FirstOrDefaultAsync(b => b.Id == transferRequest.ToBedId);
                if (toBed == null || toBed.Status != "Free")
                {
                    throw new System.InvalidOperationException("Giường đích hiện không ở trạng thái trống (Free).");
                }

                toBed.Status = "Pending Transfer";

                await _context.Requests.AddAsync(request);
                await _context.SaveChangesAsync();

                transferRequest.RequestId = request.Id;
                await _context.BedTransferRequests.AddAsync(transferRequest);

                log.RequestId = request.Id;
                await _context.RequestLogs.AddAsync(log);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return request;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<List<Guard>> GetGuardsAsync(int? dormId = null)
        {
            var query = _context.Guards
                .AsNoTracking()
                .Include(g => g.User)
                .Include(g => g.Dorm)
                .Where(g => g.User.AccountStatus == "Active" && g.User.DeletedAt == null);

            if (dormId.HasValue && dormId.Value > 0)
            {
                query = query.Where(g => g.DormId == dormId.Value);
            }

            return await query
                .OrderBy(g => g.Dorm.Name)
                .ThenBy(g => g.User.FullName)
                .ToListAsync();
        }

        public async Task<Guard?> GetGuardByUserIdAsync(int guardUserId)
        {
            return await _context.Guards
                .AsNoTracking()
                .Include(g => g.User)
                .Include(g => g.Dorm)
                .FirstOrDefaultAsync(g => g.UserId == guardUserId && g.User.AccountStatus == "Active" && g.User.DeletedAt == null);
        }

        public async Task<decimal> GetStudentPaidRoomAmountAsync(int studentId, int termId)
        {
            return await _context.Invoices
                .AsNoTracking()
                .Where(i => i.StudentId == studentId
                         && i.TermId == termId
                         && i.Status == "Paid"
                         && (i.InvoiceType == "Booking" || i.InvoiceType == "BedTransfer"))
                .SumAsync(i => (decimal?)i.Amount) ?? 0m;
        }

        public async Task<Dictionary<string, decimal>> GetUtilityPricesByTermAsync(int termId)
        {
            var prices = await _context.UtilityPrices
                .AsNoTracking()
                .Where(u => u.TermId == termId)
                .ToListAsync();

            return prices
                .GroupBy(u => u.MeterType)
                .ToDictionary(g => g.Key, g => g.First().UnitPrice);
        }

        public async Task<Dictionary<string, decimal>> GetLatestRoomMeterReadingsAsync(int roomId)
        {
            var result = new Dictionary<string, decimal>
            {
                ["Electricity"] = 0m,
                ["Water"] = 0m
            };

            foreach (var meterType in new[] { "Electricity", "Water" })
            {
                var latest = await _context.MeterReadings
                    .AsNoTracking()
                    .Where(m => m.RoomId == roomId && m.MeterType == meterType)
                    .OrderByDescending(m => m.RecordedTime)
                    .ThenByDescending(m => m.Id)
                    .Select(m => (decimal?)m.CurrentValue)
                    .FirstOrDefaultAsync();

                if (latest.HasValue)
                {
                    result[meterType] = latest.Value;
                }
            }

            return result;
        }

        public async Task<int> GetActiveRoomResidentCountAsync(int roomId, int termId)
        {
            var count = await _context.Residencies
                .AsNoTracking()
                .CountAsync(r => r.Bed.RoomId == roomId && r.TermId == termId && r.Status == "Active");
            return count > 0 ? count : 1;
        }

        public async Task<List<Equipment>> GetRoomAndBedEquipmentsAsync(int roomId, int bedId)
        {
            return await _context.Equipments
                .AsNoTracking()
                .Include(e => e.Catalog)
                .Where(e => e.RoomId == roomId && (e.BedId == null || e.BedId == bedId))
                .OrderBy(e => e.BedId.HasValue ? 1 : 0)
                .ThenBy(e => e.Catalog.Category)
                .ThenBy(e => e.AssetCode)
                .ToListAsync();
        }

        private async Task EnsureManagerExistsAsync(int managerId)
        {
            var exists = await _context.Managers.AsNoTracking().AnyAsync(m => m.UserId == managerId);
            if (!exists)
            {
                throw new System.InvalidOperationException("Tài khoản người xử lý chưa có hồ sơ Quản lý (Manager) trong hệ thống.");
            }
        }

        private void MarkModifiedIfDetached<TEntity>(TEntity entity) where TEntity : class
        {
            var entry = _context.Entry(entity);
            if (entry.State == EntityState.Detached)
            {
                _context.Attach(entity);
                entry.State = EntityState.Modified;
            }
        }

        public async Task ApproveBedTransferStep1Async(
            Request request,
            BedTransferRequest transferRequest,
            RequestLog log,
            List<Notification> notifications)
        {
            await EnsureManagerExistsAsync(request.ProcessedById ?? 0);
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                MarkModifiedIfDetached(request);
                MarkModifiedIfDetached(transferRequest);
                await _context.RequestLogs.AddAsync(log);
                if (notifications.Count > 0)
                {
                    await _context.Notifications.AddRangeAsync(notifications);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task RejectBedTransferAsync(Request request, RequestLog log, Notification notification)
        {
            await EnsureManagerExistsAsync(request.ProcessedById ?? 0);
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                if (request.BedTransferRequest != null)
                {
                    var toBed = await _context.Beds.FirstOrDefaultAsync(b => b.Id == request.BedTransferRequest.ToBedId);
                    if (toBed != null && (toBed.Status == "Pending Transfer" || toBed.Status == "Holding"))
                    {
                        toBed.Status = "Free";
                    }
                }

                MarkModifiedIfDetached(request);
                await _context.RequestLogs.AddAsync(log);
                await _context.Notifications.AddAsync(notification);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<List<Request>> GetTransferInspectionTasksForGuardAsync(int guardUserId)
        {
            return await _context.Requests
                .AsSplitQuery()
                .Include(r => r.Term).ThenInclude(t => t.UtilityPrices)
                .Include(r => r.Student).ThenInclude(s => s.User)
                .Include(r => r.BedTransferRequest).ThenInclude(bt => bt!.FromResidency)
                .Include(r => r.BedTransferRequest).ThenInclude(bt => bt!.FromBed).ThenInclude(b => b.Room).ThenInclude(rm => rm.Block).ThenInclude(bl => bl.Dorm)
                .Include(r => r.BedTransferRequest).ThenInclude(bt => bt!.FromBed).ThenInclude(b => b.Room).ThenInclude(rm => rm.Beds).ThenInclude(bd => bd.Residencies)
                .Include(r => r.BedTransferRequest).ThenInclude(bt => bt!.ToBed).ThenInclude(b => b.Room).ThenInclude(rm => rm.Block).ThenInclude(bl => bl.Dorm)
                .Include(r => r.BedTransferRequest).ThenInclude(bt => bt!.AssignedGuard).ThenInclude(g => g!.User)
                .Include(r => r.BedTransferRequest).ThenInclude(bt => bt!.MeterReadings)
                .Include(r => r.Handover).ThenInclude(h => h!.Guard).ThenInclude(g => g.User)
                .Include(r => r.Handover).ThenInclude(h => h!.HandoverItems).ThenInclude(hi => hi.Equipment).ThenInclude(eq => eq.Catalog)
                .Where(r => r.RequestType == "BedTransfer"
                         && (r.Status == "Approved" || r.Status == "Completed")
                         && r.BedTransferRequest != null
                         && r.BedTransferRequest.AssignedGuardId == guardUserId)
                .OrderBy(r => r.Handover != null ? 1 : 0)
                .ThenBy(r => r.BedTransferRequest!.TransferDate)
                .ThenByDescending(r => r.CreatedTime)
                .ToListAsync();
        }

        public async Task SubmitTransferInspectionAsync(
            Request request,
            Handover handover,
            List<HandoverItem> items,
            List<MeterReading> meterReadings,
            List<(int EquipmentId, string Condition, string? Note)> equipmentUpdates,
            RequestLog log,
            List<Notification> notifications)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                await _context.Handovers.AddAsync(handover);
                await _context.SaveChangesAsync();

                foreach (var item in items)
                {
                    item.HandoverId = handover.Id;
                }
                if (items.Count > 0)
                {
                    await _context.HandoverItems.AddRangeAsync(items);
                }

                if (meterReadings.Count > 0)
                {
                    await _context.MeterReadings.AddRangeAsync(meterReadings);
                }

                if (equipmentUpdates.Count > 0)
                {
                    var ids = equipmentUpdates.Select(u => u.EquipmentId).ToList();
                    var trackedEquipments = await _context.Equipments
                        .Where(e => ids.Contains(e.Id))
                        .ToListAsync();

                    foreach (var eq in trackedEquipments)
                    {
                        var upd = equipmentUpdates.First(u => u.EquipmentId == eq.Id);
                        eq.Condition = upd.Condition;
                        if (!string.IsNullOrWhiteSpace(upd.Note))
                        {
                            eq.Note = upd.Note;
                        }
                    }
                }

                log.RequestId = request.Id;
                await _context.RequestLogs.AddAsync(log);
                if (notifications.Count > 0)
                {
                    await _context.Notifications.AddRangeAsync(notifications);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task CompleteBedTransferAsync(
            Request request,
            BedTransferRequest transferRequest,
            int fromResidencyId,
            int fromBedId,
            int toBedId,
            DateOnly checkOutDate,
            Residency newResidency,
            List<Invoice> invoices,
            int? handoverId,
            int managerId,
            string? managerNote,
            RequestLog log,
            Notification studentNotification,
            List<Notification>? invoiceNotifications = null)
        {
            await EnsureManagerExistsAsync(managerId);
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var fromResidency = await _context.Residencies.FirstOrDefaultAsync(r => r.Id == fromResidencyId)
                    ?? throw new System.InvalidOperationException("Không tìm thấy đợt lưu trú cũ của sinh viên.");
                var fromBed = await _context.Beds.FirstOrDefaultAsync(b => b.Id == fromBedId)
                    ?? throw new System.InvalidOperationException("Không tìm thấy giường cũ.");
                var toBed = await _context.Beds.FirstOrDefaultAsync(b => b.Id == toBedId)
                    ?? throw new System.InvalidOperationException("Không tìm thấy giường đích.");

                if (fromResidency.Status != "Active")
                {
                    throw new System.InvalidOperationException("Đợt lưu trú cũ không còn ở trạng thái Active.");
                }

                // 1. Kết thúc lưu trú cũ và SaveChanges trước để giải phóng UX_Residency_Student_Term
                fromResidency.Status = "Ended";
                fromResidency.ActualCheckOutDate = fromResidency.CheckInDate.HasValue && checkOutDate < fromResidency.CheckInDate.Value
                    ? fromResidency.CheckInDate.Value
                    : checkOutDate;
                fromBed.Status = "Free";
                await _context.SaveChangesAsync();

                // 2. Tạo lưu trú mới & cập nhật giường đích
                await _context.Residencies.AddAsync(newResidency);
                toBed.Status = "Occupied";
                await _context.SaveChangesAsync();

                transferRequest.NewResidencyId = newResidency.Id;
                MarkModifiedIfDetached(transferRequest);

                // 3. Tạo hóa đơn (Unpaid) kèm chi tiết hạng mục và thông báo Invoice gắn đúng Invoice.Id
                if (invoices.Count > 0)
                {
                    await _context.Invoices.AddRangeAsync(invoices);
                    if (invoiceNotifications != null && invoiceNotifications.Count > 0)
                    {
                        await _context.SaveChangesAsync();
                        for (var i = 0; i < invoices.Count && i < invoiceNotifications.Count; i++)
                        {
                            invoiceNotifications[i].ReferenceId = invoices[i].Id.ToString();
                        }
                        await _context.Notifications.AddRangeAsync(invoiceNotifications);
                    }
                }

                // 4. Cập nhật thông tin review của Manager trên Handover
                if (handoverId.HasValue)
                {
                    var trackedHandover = await _context.Handovers.FirstOrDefaultAsync(h => h.Id == handoverId.Value);
                    if (trackedHandover != null)
                    {
                        trackedHandover.ManagerReviewedById = managerId;
                        trackedHandover.ManagerReviewedTime = System.DateTime.UtcNow;
                        if (!string.IsNullOrWhiteSpace(managerNote))
                        {
                            trackedHandover.ManagerNote = managerNote;
                        }
                    }
                }

                // 5. Hoàn tất Request, ghi Log & Notification
                MarkModifiedIfDetached(request);
                await _context.RequestLogs.AddAsync(log);
                await _context.Notifications.AddAsync(studentNotification);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
}

