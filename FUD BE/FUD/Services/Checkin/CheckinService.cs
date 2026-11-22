using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FUD.DTOs.Checkin;
using FUD.Models;
using FUD.Services.Interfaces;
using FUD.Services.Interfaces.Checkin;
using Microsoft.EntityFrameworkCore;

namespace FUD.Services.Checkin
{
    public class CheckinService : ICheckinService
    {
        private readonly FudContext _context;
        private readonly IInAppNotificationService? _inAppNotificationService;

        public CheckinService(FudContext context, IInAppNotificationService? inAppNotificationService = null)
        {
            _context = context;
            _inAppNotificationService = inAppNotificationService;
        }

        private class CheckinMetaNote
        {
            public string? Title { get; set; }
            public string? OtherItems { get; set; }
            public string? ElecL { get; set; }
            public string? ElecR { get; set; }
            public string? WaterL { get; set; }
            public string? WaterR { get; set; }
        }

        private async Task<int> ResolveGuardDormIdOrThrowAsync(int guardUserId)
        {
            var guard = await _context.Guards
                .AsNoTracking()
                .FirstOrDefaultAsync(g => g.UserId == guardUserId);

            if (guard == null || guard.DormId <= 0)
            {
                throw new UnauthorizedAccessException("Tài khoản bảo vệ chưa được phân công khu KTX.");
            }

            return guard.DormId;
        }

        public async Task<List<PendingCheckinStudentDto>> GetPendingStudentsAsync(
            int? blockId = null,
            int? roomId = null,
            string? search = null,
            int? guardUserId = null)
        {
            int? guardDormId = null;
            if (guardUserId.HasValue)
            {
                guardDormId = await ResolveGuardDormIdOrThrowAsync(guardUserId.Value);
            }

            var result = new List<PendingCheckinStudentDto>();

            // 1. Các Residency đang ở trạng thái Reserved / Active nhưng chưa lập biên bản Checkin (Handover == null)
            var residencyQuery = _context.Residencies
                .AsNoTracking()
                .Include(r => r.Student).ThenInclude(s => s.User)
                .Include(r => r.Bed).ThenInclude(b => b.Room).ThenInclude(rm => rm.Block)
                .Include(r => r.Term)
                .Include(r => r.Handover)
                .Where(r => (r.Status == "Reserved" || r.Status == "Active") && r.Handover == null);

            if (guardDormId.HasValue)
            {
                residencyQuery = residencyQuery.Where(r => r.Bed.Room.Block.DormId == guardDormId.Value);
            }

            var residencies = await residencyQuery.ToListAsync();

            foreach (var r in residencies)
            {
                if (r.Bed?.Room == null || r.Student?.User == null) continue;

                result.Add(new PendingCheckinStudentDto
                {
                    BookingRequestId = r.SourceBookingId,
                    ResidencyId = r.Id,
                    StudentId = r.StudentId,
                    StudentCode = r.Student.User.Code,
                    StudentName = r.Student.User.FullName,
                    Gender = r.Student.User.Gender,
                    Phone = r.Student.User.Phone,
                    Email = r.Student.User.Email,
                    Cohort = r.Student.Cohort,
                    Major = r.Student.Major,
                    BlockId = r.Bed.Room.BlockId,
                    BlockCode = r.Bed.Room.Block?.Code ?? string.Empty,
                    BlockName = r.Bed.Room.Block?.Name ?? string.Empty,
                    RoomId = r.Bed.RoomId,
                    RoomNumber = r.Bed.Room.RoomNumber,
                    BedId = r.BedId,
                    BedCode = r.Bed.BedCode,
                    TermId = r.TermId,
                    TermName = r.Term != null ? $"{r.Term.Name} ({r.Term.AcademicYear})" : string.Empty,
                    BookingStatus = r.Status == "Active" ? "Chờ lập biên bản bàn giao" : "Đã xếp giường - Chờ nhận phòng"
                });
            }

            // 2. Các đơn Booking_Requests đã Approved/Completed nhưng chưa tạo dòng Residency
            var existingKeys = result
                .Select(x => (x.StudentId, x.TermId))
                .ToHashSet();

            var approvedBookingQuery = _context.BookingRequests
                .AsNoTracking()
                .Include(br => br.Student).ThenInclude(s => s.User)
                .Include(br => br.Term)
                .Include(br => br.Bed).ThenInclude(b => b.Room).ThenInclude(rm => rm.Block)
                .Where(br => br.Status == "Approved" || br.Status == "Completed");

            if (guardDormId.HasValue)
            {
                approvedBookingQuery = approvedBookingQuery.Where(br => br.Bed.Room.Block.DormId == guardDormId.Value);
            }

            var approvedBookings = await approvedBookingQuery.ToListAsync();

            // Kiểm tra những sinh viên đã có Handover trong kỳ đó để không liệt kê lại
            var checkedInKeys = (await _context.Residencies
                .AsNoTracking()
                .Where(r => r.Handover != null)
                .Select(r => new { r.StudentId, r.TermId })
                .ToListAsync())
                .Select(x => (x.StudentId, x.TermId))
                .ToHashSet();

            foreach (var br in approvedBookings)
            {
                if (br.Student?.User == null || br.Bed?.Room == null) continue;

                var key = (br.StudentId, br.TermId);
                if (existingKeys.Contains(key) || checkedInKeys.Contains(key)) continue;

                existingKeys.Add(key);
                result.Add(new PendingCheckinStudentDto
                {
                    BookingRequestId = br.Id,
                    ResidencyId = null,
                    StudentId = br.StudentId,
                    StudentCode = br.Student.User.Code,
                    StudentName = br.Student.User.FullName,
                    Gender = br.Student.User.Gender,
                    Phone = br.Student.User.Phone,
                    Email = br.Student.User.Email,
                    Cohort = br.Student.Cohort,
                    Major = br.Student.Major,
                    BlockId = br.Bed.Room.BlockId,
                    BlockCode = br.Bed.Room.Block?.Code ?? string.Empty,
                    BlockName = br.Bed.Room.Block?.Name ?? string.Empty,
                    RoomId = br.Bed.RoomId,
                    RoomNumber = br.Bed.Room.RoomNumber,
                    BedId = br.BedId,
                    BedCode = br.Bed.BedCode,
                    TermId = br.TermId,
                    TermName = br.Term != null ? $"{br.Term.Name} ({br.Term.AcademicYear})" : string.Empty,
                    BookingStatus = "Đơn đặt phòng đã duyệt - Chờ Check-in"
                });
            }

            if (blockId.HasValue)
            {
                result = result.Where(x => x.BlockId == blockId.Value).ToList();
            }

            if (roomId.HasValue)
            {
                result = result.Where(x => x.RoomId == roomId.Value).ToList();
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                result = result.Where(x =>
                    x.StudentCode.ToLower().Contains(s) ||
                    x.StudentName.ToLower().Contains(s) ||
                    x.RoomNumber.ToLower().Contains(s) ||
                    x.BedCode.ToLower().Contains(s)).ToList();
            }

            return result
                .OrderBy(x => x.BlockCode)
                .ThenBy(x => x.RoomNumber)
                .ThenBy(x => x.BedCode)
                .ToList();
        }

        public async Task<List<RoomEquipmentCheckinDto>> GetRoomEquipmentsAsync(int roomId, int? bedId = null, int? guardUserId = null)
        {
            if (guardUserId.HasValue)
            {
                var guardDormId = await ResolveGuardDormIdOrThrowAsync(guardUserId.Value);
                var roomDormId = await _context.Rooms
                    .AsNoTracking()
                    .Where(r => r.Id == roomId)
                    .Select(r => (int?)r.Block.DormId)
                    .FirstOrDefaultAsync();

                if (!roomDormId.HasValue || roomDormId.Value != guardDormId)
                {
                    throw new UnauthorizedAccessException("Bạn chỉ được xem thiết bị của phòng thuộc khu KTX mình phụ trách.");
                }
            }

            var query = _context.Equipments
                .AsNoTracking()
                .Include(e => e.Catalog)
                .Include(e => e.Bed)
                .Where(e => e.RoomId == roomId && e.Condition != "Liquidated");

            // Trong FUD_V6: Thiết bị chung của phòng có BedId == null; Thiết bị cá nhân (Tủ quần áo, Bàn học) gắn theo từng BedId
            if (bedId.HasValue && bedId.Value > 0)
            {
                query = query.Where(e => e.BedId == null || e.BedId == bedId.Value);
            }

            var equipments = await query
                .OrderBy(e => e.BedId.HasValue ? 0 : 1)
                .ThenBy(e => e.Catalog.Category)
                .ThenBy(e => e.Catalog.Name)
                .ThenBy(e => e.AssetCode)
                .ToListAsync();

            return equipments.Select(e => new RoomEquipmentCheckinDto
            {
                EquipmentId = e.Id,
                CatalogId = e.CatalogId,
                CatalogName = e.Catalog?.Name ?? "Thiết bị",
                Category = e.Catalog?.Category ?? "Chung",
                AssetCode = e.AssetCode,
                BedId = e.BedId,
                BedCode = e.Bed?.BedCode,
                CurrentCondition = e.Condition == "Good" ? "Good" : "Damaged",
                Note = !string.IsNullOrWhiteSpace(e.Note)
                    ? e.Note
                    : e.Condition == "Under repair"
                        ? "Đang sửa chữa"
                        : e.Condition == "Broken"
                            ? "Hư hỏng"
                            : "Bình thường"
            }).ToList();
        }

        public async Task<CheckinHandoverResponseDto> CompleteCheckinAsync(int guardUserId, CreateCheckinHandoverDto dto)
        {
            if (dto.StudentId <= 0 || dto.RoomId <= 0 || dto.BedId <= 0)
            {
                throw new ArgumentException("Thông tin sinh viên, phòng và giường bàn giao không hợp lệ.");
            }

            var guardDormId = await ResolveGuardDormIdOrThrowAsync(guardUserId);

            var now = DateTime.UtcNow.AddHours(7);
            var today = DateOnly.FromDateTime(now);

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. Tìm hoặc khởi tạo Residency từ đơn đặt phòng đã duyệt (kiểm tra phạm vi khu KTX trước khi ghi dữ liệu)
                Residency? residency = null;

                if (dto.ResidencyId.HasValue && dto.ResidencyId.Value > 0)
                {
                    residency = await _context.Residencies
                        .Include(r => r.Bed).ThenInclude(b => b.Room).ThenInclude(rm => rm.Block)
                        .Include(r => r.Handover)
                        .FirstOrDefaultAsync(r => r.Id == dto.ResidencyId.Value);
                }

                if (residency == null && dto.BookingRequestId.HasValue && dto.BookingRequestId.Value > 0)
                {
                    var booking = await _context.BookingRequests
                        .Include(br => br.Bed).ThenInclude(b => b.Room).ThenInclude(rm => rm.Block)
                        .FirstOrDefaultAsync(br => br.Id == dto.BookingRequestId.Value);

                    if (booking == null || booking.StudentId != dto.StudentId)
                    {
                        throw new ArgumentException("Không tìm thấy đơn đặt phòng hợp lệ của sinh viên này.");
                    }

                    var bookingDormId = booking.Bed?.Room?.Block?.DormId;
                    if (!bookingDormId.HasValue || bookingDormId.Value != guardDormId)
                    {
                        throw new UnauthorizedAccessException("Bạn chỉ được thực hiện Check-in cho phòng thuộc khu KTX mình phụ trách.");
                    }

                    residency = await _context.Residencies
                        .Include(r => r.Bed).ThenInclude(b => b.Room).ThenInclude(rm => rm.Block)
                        .Include(r => r.Handover)
                        .FirstOrDefaultAsync(r => r.StudentId == dto.StudentId && r.TermId == booking.TermId && (r.Status == "Reserved" || r.Status == "Active"));

                    if (residency == null)
                    {
                        residency = new Residency
                        {
                            StudentId = dto.StudentId,
                            BedId = booking.BedId,
                            TermId = booking.TermId,
                            SourceBookingId = booking.Id,
                            RegistrationType = booking.BookingKind,
                            CheckInDate = today,
                            Status = "Active"
                        };
                        _context.Residencies.Add(residency);
                        await _context.SaveChangesAsync();
                    }
                }

                if (residency == null)
                {
                    throw new ArgumentException("Sinh viên không có đơn đặt phòng hoặc hồ sơ cư trú chờ Check-in.");
                }

                if (residency.Handover != null)
                {
                    throw new InvalidOperationException("Sinh viên này đã được lập biên bản bàn giao Check-in trước đó.");
                }

                var bed = residency.Bed
                    ?? await _context.Beds
                        .Include(b => b.Room).ThenInclude(rm => rm.Block)
                        .FirstOrDefaultAsync(b => b.Id == residency.BedId);
                if (bed == null)
                {
                    throw new ArgumentException("Không tìm thấy thông tin giường bàn giao.");
                }

                var targetRoomDormId = bed.Room?.Block?.DormId
                    ?? await _context.Rooms
                        .AsNoTracking()
                        .Where(r => r.Id == bed.RoomId)
                        .Select(r => (int?)r.Block.DormId)
                        .FirstOrDefaultAsync();

                if (!targetRoomDormId.HasValue || targetRoomDormId.Value != guardDormId)
                {
                    throw new UnauthorizedAccessException("Bạn chỉ được thực hiện Check-in cho phòng thuộc khu KTX mình phụ trách.");
                }

                // 2. Cập nhật trạng thái Residency, Bed và Request
                residency.Status = "Active";
                residency.CheckInDate ??= today;
                bed.Status = "Occupied";
                var targetRoomId = bed.RoomId;

                var reqId = dto.BookingRequestId ?? residency.SourceBookingId;
                if (reqId.HasValue)
                {
                    var bookingReq = await _context.BookingRequests.FirstOrDefaultAsync(b => b.Id == reqId.Value);
                    if (bookingReq != null && bookingReq.Status != "Completed")
                    {
                        bookingReq.Status = "Completed";
                        bookingReq.ProcessedTime = now;
                    }
                    else
                    {
                        var req = await _context.Requests.FirstOrDefaultAsync(r => r.Id == reqId.Value);
                        if (req != null && req.Status != "Completed")
                        {
                            var oldStatus = req.Status;
                            req.Status = "Completed";
                            req.ProcessedTime = now;

                            _context.RequestLogs.Add(new RequestLog
                            {
                                RequestId = req.Id,
                                FromStatus = oldStatus,
                                ToStatus = "Completed",
                                Note = "Bảo vệ đã hoàn tất lập biên bản bàn giao nhận phòng (Check-in).",
                                ChangeTime = now,
                                ChangedById = guardUserId
                            });
                        }
                    }
                }

                // 4. Đóng gói thông tin mục 17 (Vật dụng khác, Số điện L/R, Số nước L/R, Chức danh) vào BelongingsNote
                var meta = new CheckinMetaNote
                {
                    Title = string.IsNullOrWhiteSpace(dto.StudentTitle) ? "Sinh viên" : dto.StudentTitle.Trim(),
                    OtherItems = dto.OtherItems?.Trim(),
                    ElecL = dto.ElectricityL?.Trim(),
                    ElecR = dto.ElectricityR?.Trim(),
                    WaterL = dto.WaterL?.Trim(),
                    WaterR = dto.WaterR?.Trim()
                };
                var metaJson = JsonSerializer.Serialize(meta);
                if (metaJson.Length > 500)
                {
                    metaJson = metaJson.Substring(0, 500);
                }

                // 5. Chuẩn bị danh sách Handover_Items: chỉ lấy thiết bị chung của phòng (BedId == null) + thiết bị gắn với giường của SV (BedId == residency.BedId)
                var roomEquipments = await _context.Equipments
                    .Where(e => e.RoomId == targetRoomId
                             && (e.BedId == null || e.BedId == residency.BedId)
                             && e.Condition != "Liquidated")
                    .ToListAsync();

                var inputMap = (dto.Items ?? new List<CheckinItemInputDto>())
                    .GroupBy(i => i.EquipmentId)
                    .ToDictionary(g => g.Key, g => g.First());

                var handoverItems = new List<HandoverItem>();
                bool hasDamageOrMissing = false;

                foreach (var equip in roomEquipments)
                {
                    string cond = "Good";
                    string? note = equip.Note;

                    if (inputMap.TryGetValue(equip.Id, out var inputItem))
                    {
                        cond = inputItem.ItemCondition is "Good" or "Damaged" or "Missing"
                            ? inputItem.ItemCondition
                            : "Good";
                        note = inputItem.Note?.Trim();
                    }
                    else if (equip.Condition != "Good")
                    {
                        cond = "Damaged";
                    }

                    if (cond != "Good")
                    {
                        hasDamageOrMissing = true;
                        if (cond == "Damaged" && equip.Condition == "Good")
                        {
                            equip.Condition = "Broken";
                        }
                    }

                    handoverItems.Add(new HandoverItem
                    {
                        EquipmentId = equip.Id,
                        ItemCondition = cond,
                        Note = note,
                        CompensationAmount = 0
                    });
                }

                var handover = new Handover
                {
                    HandoverType = "Checkin",
                    RequestId = null,
                    ResidencyId = residency.Id,
                    RoomId = targetRoomId,
                    GuardId = guardUserId,
                    BelongingsRemainingFlag = !string.IsNullOrWhiteSpace(dto.OtherItems),
                    BelongingsNote = metaJson,
                    HasDamageOrMissingFlag = hasDamageOrMissing,
                    TotalCompensation = 0,
                    Status = "Completed",
                    StudentAcceptedFlag = true,
                    StudentAcceptedTime = now,
                    CreatedTime = now,
                    CompletedTime = now,
                    HandoverItems = handoverItems
                };

                _context.Handovers.Add(handover);

                _context.AuditLogs.Add(new AuditLog
                {
                    UserId = guardUserId,
                    ActionType = "CHECKIN_HANDOVER",
                    TargetEntity = "Handover",
                    TargetId = residency.Id.ToString(),
                    OldValue = null,
                    NewValue = $"Hoàn tất Check-in cho SV ID={dto.StudentId} tại Phòng ID={targetRoomId}, Giường ID={residency.BedId}",
                    CreatedAt = now
                });

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var saved = await GetCheckinByIdAsync(handover.Id);

                if (_inAppNotificationService != null)
                {
                    await _inAppNotificationService.NotifyUserAsync(
                        dto.StudentId,
                        "Hoàn tất bàn giao nhận phòng (Check-in)",
                        $"Biên bản bàn giao phòng {saved?.RoomNumber} - Giường {bed.BedCode} đã được Bảo vệ xác nhận hoàn tất.",
                        "Checkin",
                        handover.Id.ToString());
                }

                return saved!;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<List<CheckinHandoverResponseDto>> GetCheckinHistoryAsync(
            int? blockId = null,
            int? roomId = null,
            string? search = null,
            int? guardUserId = null)
        {
            int? guardDormId = null;
            if (guardUserId.HasValue)
            {
                guardDormId = await ResolveGuardDormIdOrThrowAsync(guardUserId.Value);
            }

            var query = _context.Handovers
                .AsNoTracking()
                .Include(h => h.Guard).ThenInclude(g => g.User)
                .Include(h => h.Room).ThenInclude(rm => rm.Block)
                .Include(h => h.Residency).ThenInclude(r => r!.Student).ThenInclude(s => s.User)
                .Include(h => h.Residency).ThenInclude(r => r!.Bed)
                .Include(h => h.Residency).ThenInclude(r => r!.Term)
                .Include(h => h.HandoverItems).ThenInclude(hi => hi.Equipment).ThenInclude(e => e.Catalog)
                .Include(h => h.HandoverItems).ThenInclude(hi => hi.Equipment).ThenInclude(e => e.Bed)
                .Where(h => h.HandoverType == "Checkin")
                .AsQueryable();

            if (guardDormId.HasValue)
            {
                query = query.Where(h => h.Room.Block.DormId == guardDormId.Value);
            }

            if (blockId.HasValue)
            {
                query = query.Where(h => h.Room.BlockId == blockId.Value);
            }

            if (roomId.HasValue)
            {
                query = query.Where(h => h.RoomId == roomId.Value);
            }

            var list = await query
                .OrderByDescending(h => h.CreatedTime)
                .ToListAsync();

            var mapped = list.Select(MapToResponseDto).ToList();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                mapped = mapped.Where(x =>
                    x.StudentCode.ToLower().Contains(s) ||
                    x.StudentName.ToLower().Contains(s) ||
                    x.RoomNumber.ToLower().Contains(s) ||
                    x.BlockCode.ToLower().Contains(s)).ToList();
            }

            return mapped;
        }

        public async Task<List<CheckinHandoverResponseDto>> GetStudentCheckinsAsync(int studentUserId)
        {
            var list = await _context.Handovers
                .AsNoTracking()
                .Include(h => h.Guard).ThenInclude(g => g.User)
                .Include(h => h.Room).ThenInclude(rm => rm.Block)
                .Include(h => h.Residency).ThenInclude(r => r!.Student).ThenInclude(s => s.User)
                .Include(h => h.Residency).ThenInclude(r => r!.Bed)
                .Include(h => h.Residency).ThenInclude(r => r!.Term)
                .Include(h => h.HandoverItems).ThenInclude(hi => hi.Equipment).ThenInclude(e => e.Catalog)
                .Include(h => h.HandoverItems).ThenInclude(hi => hi.Equipment).ThenInclude(e => e.Bed)
                .Where(h => h.HandoverType == "Checkin" && h.Residency != null && h.Residency.StudentId == studentUserId)
                .OrderByDescending(h => h.CreatedTime)
                .ToListAsync();

            return list.Select(MapToResponseDto).ToList();
        }

        public async Task<CheckinHandoverResponseDto?> GetCheckinByIdAsync(
            int id,
            int? callerUserId = null,
            bool isManagerOrAdmin = true,
            bool isGuard = false)
        {
            var handover = await _context.Handovers
                .AsNoTracking()
                .Include(h => h.Guard).ThenInclude(g => g.User)
                .Include(h => h.Room).ThenInclude(rm => rm.Block)
                .Include(h => h.Residency).ThenInclude(r => r!.Student).ThenInclude(s => s.User)
                .Include(h => h.Residency).ThenInclude(r => r!.Bed)
                .Include(h => h.Residency).ThenInclude(r => r!.Term)
                .Include(h => h.HandoverItems).ThenInclude(hi => hi.Equipment).ThenInclude(e => e.Catalog)
                .Include(h => h.HandoverItems).ThenInclude(hi => hi.Equipment).ThenInclude(e => e.Bed)
                .FirstOrDefaultAsync(h => h.Id == id && h.HandoverType == "Checkin");

            if (handover == null)
            {
                return null;
            }

            if (!isManagerOrAdmin)
            {
                if (isGuard)
                {
                    if (!callerUserId.HasValue)
                    {
                        throw new UnauthorizedAccessException("Không xác định được danh tính cán bộ bảo vệ.");
                    }

                    var guardDormId = await ResolveGuardDormIdOrThrowAsync(callerUserId.Value);
                    var handoverDormId = handover.Room?.Block?.DormId;
                    if (!handoverDormId.HasValue || handoverDormId.Value != guardDormId)
                    {
                        throw new UnauthorizedAccessException("Bạn không có quyền xem biên bản Check-in của khu KTX khác.");
                    }
                }
                else
                {
                    if (!callerUserId.HasValue || handover.Residency == null || handover.Residency.StudentId != callerUserId.Value)
                    {
                        throw new UnauthorizedAccessException("Bạn chỉ được xem biên bản Check-in của chính mình.");
                    }
                }
            }

            return MapToResponseDto(handover);
        }

        private static CheckinHandoverResponseDto MapToResponseDto(Handover h)
        {
            CheckinMetaNote? meta = null;
            if (!string.IsNullOrWhiteSpace(h.BelongingsNote))
            {
                try
                {
                    meta = JsonSerializer.Deserialize<CheckinMetaNote>(h.BelongingsNote);
                }
                catch
                {
                    meta = new CheckinMetaNote { OtherItems = h.BelongingsNote };
                }
            }

            var studentUser = h.Residency?.Student?.User;
            var guardUser = h.Guard?.User;
            var bed = h.Residency?.Bed;
            var room = h.Room;
            var block = room?.Block;
            var term = h.Residency?.Term;

            return new CheckinHandoverResponseDto
            {
                Id = h.Id,
                ResidencyId = h.ResidencyId ?? 0,
                StudentId = h.Residency?.StudentId ?? 0,
                StudentCode = studentUser?.Code ?? string.Empty,
                StudentName = studentUser?.FullName ?? string.Empty,
                StudentTitle = meta?.Title ?? "Sinh viên",
                GuardId = h.GuardId,
                GuardCode = guardUser?.Code ?? string.Empty,
                GuardName = guardUser?.FullName ?? string.Empty,
                BlockId = room?.BlockId ?? 0,
                BlockCode = block?.Code ?? string.Empty,
                BlockName = block?.Name ?? string.Empty,
                RoomId = h.RoomId,
                RoomNumber = room?.RoomNumber ?? string.Empty,
                BedId = bed?.Id ?? 0,
                BedCode = bed?.BedCode ?? string.Empty,
                TermName = term != null ? $"{term.Name} ({term.AcademicYear})" : string.Empty,
                Status = h.Status,
                HasDamageOrMissingFlag = h.HasDamageOrMissingFlag,
                OtherItems = meta?.OtherItems,
                ElectricityL = meta?.ElecL,
                ElectricityR = meta?.ElecR,
                WaterL = meta?.WaterL,
                WaterR = meta?.WaterR,
                CreatedTime = h.CreatedTime,
                CompletedTime = h.CompletedTime,
                Items = h.HandoverItems
                    .OrderBy(i => i.Equipment?.BedId.HasValue == true ? 0 : 1)
                    .ThenBy(i => i.Equipment?.Catalog?.Category)
                    .ThenBy(i => i.Equipment?.Catalog?.Name)
                    .Select(i => new CheckinHandoverItemResponseDto
                    {
                        Id = i.Id,
                        EquipmentId = i.EquipmentId,
                        CatalogName = i.Equipment?.Catalog?.Name ?? "Thiết bị",
                        Category = i.Equipment?.Catalog?.Category ?? "Chung",
                        AssetCode = i.Equipment?.AssetCode ?? string.Empty,
                        BedCode = i.Equipment?.Bed?.BedCode,
                        Quantity = 1,
                        ItemCondition = i.ItemCondition,
                        Note = i.Note
                    }).ToList()
            };
        }
    }
}
