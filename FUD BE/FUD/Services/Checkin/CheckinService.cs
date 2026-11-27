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

            if (result.Count > 0)
            {
                await PopulateCheckoutBlockStatusAsync(result, residencies);
            }

            return result
                .OrderBy(x => x.BlockCode)
                .ThenBy(x => x.RoomNumber)
                .ThenBy(x => x.BedCode)
                .ToList();
        }

        private async Task PopulateCheckoutBlockStatusAsync(
            List<PendingCheckinStudentDto> items,
            List<Residency> loadedResidencies)
        {
            var studentIds = items.Select(x => x.StudentId).Distinct().ToList();
            var registrationTypeByResidencyId = loadedResidencies.ToDictionary(r => r.Id, r => r.RegistrationType);

            var pendingCheckoutRequests = await _context.Requests
                .AsNoTracking()
                .Include(r => r.Handover)
                .Where(r => studentIds.Contains(r.StudentId)
                            && (r.RequestType == "Checkout" || r.RequestType == "BedTransfer")
                            && r.Status == "Approved"
                            && (r.Handover == null || r.Handover.Status != "Completed"))
                .ToListAsync();

            var activeResidencies = await _context.Residencies
                .AsNoTracking()
                .Include(r => r.Bed).ThenInclude(b => b.Room)
                .Where(r => studentIds.Contains(r.StudentId) && r.Status == "Active")
                .ToListAsync();

            var transferResidencyIds = loadedResidencies
                .Where(r => string.Equals(r.RegistrationType, "Transfer", StringComparison.OrdinalIgnoreCase))
                .Select(r => r.Id)
                .ToList();

            var transferByNewResidencyId = transferResidencyIds.Count > 0
                ? await _context.BedTransferRequests
                    .AsNoTracking()
                    .Include(bt => bt.FromResidency)
                    .Include(bt => bt.Request).ThenInclude(req => req.Handover)
                    .Where(bt => bt.NewResidencyId.HasValue && transferResidencyIds.Contains(bt.NewResidencyId.Value))
                    .ToDictionaryAsync(bt => bt.NewResidencyId!.Value)
                : new Dictionary<int, BedTransferRequest>();

            foreach (var item in items)
            {
                var pendingReq = pendingCheckoutRequests.FirstOrDefault(r => r.StudentId == item.StudentId);
                if (pendingReq != null)
                {
                    var reqLabel = pendingReq.RequestType == "BedTransfer" ? "chuyển giường" : "trả phòng";
                    item.CanCheckin = false;
                    item.CheckinBlockedReason =
                        $"Sinh viên đang có đơn {reqLabel} (REQ-{pendingReq.Id:D6}) chưa được Bảo vệ hoàn tất biên bản Check-out phòng cũ.";
                    continue;
                }

                if (item.ResidencyId.HasValue
                    && registrationTypeByResidencyId.TryGetValue(item.ResidencyId.Value, out var regType)
                    && string.Equals(regType, "Transfer", StringComparison.OrdinalIgnoreCase)
                    && transferByNewResidencyId.TryGetValue(item.ResidencyId.Value, out var bt))
                {
                    var oldEnded = string.Equals(bt.FromResidency?.Status, "Ended", StringComparison.OrdinalIgnoreCase);
                    var handoverDone = bt.Request?.Handover != null
                        && string.Equals(bt.Request.Handover.HandoverType, "BedTransfer", StringComparison.OrdinalIgnoreCase)
                        && string.Equals(bt.Request.Handover.Status, "Completed", StringComparison.OrdinalIgnoreCase);

                    if (!oldEnded || !handoverDone)
                    {
                        item.CanCheckin = false;
                        item.CheckinBlockedReason =
                            $"Bảo vệ tòa cũ chưa hoàn tất biên bản kiểm tra/Check-out phòng cũ cho đơn chuyển giường REQ-{bt.RequestId:D6}.";
                        continue;
                    }
                }

                // Không chặn trường hợp Keeping giữ nguyên cùng giường (oldRes.BedId == item.BedId) sang kỳ kế tiếp.
                var otherActive = activeResidencies.FirstOrDefault(r =>
                    r.StudentId == item.StudentId
                    && (!item.ResidencyId.HasValue || r.Id != item.ResidencyId.Value)
                    && r.BedId != item.BedId);

                if (otherActive != null)
                {
                    var oldRoom = otherActive.Bed?.Room?.RoomNumber ?? "cũ";
                    var oldBed = otherActive.Bed?.BedCode ?? $"#{otherActive.BedId}";
                    item.CanCheckin = false;
                    item.CheckinBlockedReason =
                        $"Sinh viên vẫn đang lưu trú tại P.{oldRoom} - Giường {oldBed}. Bảo vệ cần hoàn tất Check-out phòng cũ trước khi lập đơn Check-in.";
                }
            }
        }

        private async Task<string?> GetCheckoutBlockReasonAsync(
            int studentId,
            int? targetResidencyId,
            int targetBedId,
            string? registrationType)
        {
            var pendingReq = await _context.Requests
                .AsNoTracking()
                .Include(r => r.Handover)
                .FirstOrDefaultAsync(r =>
                    r.StudentId == studentId
                    && (r.RequestType == "Checkout" || r.RequestType == "BedTransfer")
                    && r.Status == "Approved"
                    && (r.Handover == null || r.Handover.Status != "Completed"));

            if (pendingReq != null)
            {
                var reqLabel = pendingReq.RequestType == "BedTransfer" ? "chuyển giường" : "trả phòng";
                return $"Sinh viên đang có đơn {reqLabel} (REQ-{pendingReq.Id:D6}) chưa được Bảo vệ hoàn tất biên bản Check-out phòng cũ.";
            }

            if (targetResidencyId.HasValue
                && string.Equals(registrationType, "Transfer", StringComparison.OrdinalIgnoreCase))
            {
                var bt = await _context.BedTransferRequests
                    .AsNoTracking()
                    .Include(x => x.FromResidency)
                    .Include(x => x.Request).ThenInclude(req => req.Handover)
                    .FirstOrDefaultAsync(x => x.NewResidencyId == targetResidencyId.Value);

                if (bt != null)
                {
                    var oldEnded = string.Equals(bt.FromResidency?.Status, "Ended", StringComparison.OrdinalIgnoreCase);
                    var handoverDone = bt.Request?.Handover != null
                        && string.Equals(bt.Request.Handover.HandoverType, "BedTransfer", StringComparison.OrdinalIgnoreCase)
                        && string.Equals(bt.Request.Handover.Status, "Completed", StringComparison.OrdinalIgnoreCase);

                    if (!oldEnded || !handoverDone)
                    {
                        return $"Bảo vệ tòa cũ chưa hoàn tất biên bản kiểm tra/Check-out phòng cũ cho đơn chuyển giường REQ-{bt.RequestId:D6}.";
                    }
                }
            }

            // Không chặn trường hợp Keeping giữ nguyên cùng giường (r.BedId == targetBedId) sang kỳ kế tiếp.
            var otherActive = await _context.Residencies
                .AsNoTracking()
                .Include(r => r.Bed).ThenInclude(b => b.Room)
                .FirstOrDefaultAsync(r =>
                    r.StudentId == studentId
                    && r.Status == "Active"
                    && (!targetResidencyId.HasValue || r.Id != targetResidencyId.Value)
                    && r.BedId != targetBedId);

            if (otherActive != null)
            {
                var oldRoom = otherActive.Bed?.Room?.RoomNumber ?? "cũ";
                var oldBed = otherActive.Bed?.BedCode ?? $"#{otherActive.BedId}";
                return $"Sinh viên vẫn đang lưu trú tại P.{oldRoom} - Giường {oldBed}. Bảo vệ cần hoàn tất Check-out phòng cũ trước khi lập đơn Check-in.";
            }

            return null;
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
                            CheckInDate = null,
                            Status = "Reserved"
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

                var checkoutBlockReason = await GetCheckoutBlockReasonAsync(
                    residency.StudentId,
                    residency.Id,
                    bed.Id,
                    residency.RegistrationType);

                if (!string.IsNullOrEmpty(checkoutBlockReason))
                {
                    throw new InvalidOperationException(checkoutBlockReason);
                }

                var targetRoomId = bed.RoomId;

                // 4. Đóng gói thông tin bổ sung (Chức danh, Vật dụng khác) vào BelongingsNote
                var meta = new CheckinMetaNote
                {
                    Title = string.IsNullOrWhiteSpace(dto.StudentTitle) ? "Sinh viên" : dto.StudentTitle.Trim(),
                    OtherItems = dto.OtherItems?.Trim()
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

                // Luồng Check-in (không còn bước Sinh viên ký online):
                // - Nếu có thiết bị hỏng/thiếu -> Status = "Draft" (chờ Manager Troubleshooting -> Guard Confirm Check-in)
                // - Nếu thiết bị đầy đủ (tất cả Good) -> Status = "Completed" & kích hoạt Residency/Bed/Booking ngay (chờ Manager Check record)
                var initialStatus = hasDamageOrMissing ? "Draft" : "Completed";

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
                    Status = initialStatus,
                    StudentAcceptedFlag = !hasDamageOrMissing,
                    StudentAcceptedTime = hasDamageOrMissing ? null : now,
                    CreatedTime = now,
                    CompletedTime = hasDamageOrMissing ? null : now,
                    HandoverItems = handoverItems
                };

                _context.Handovers.Add(handover);

                if (!hasDamageOrMissing)
                {
                    residency.Status = "Active";
                    residency.CheckInDate ??= today;
                    bed.Status = "Occupied";

                    if (residency.SourceBookingId.HasValue)
                    {
                        var bookingReq = await _context.BookingRequests
                            .FirstOrDefaultAsync(b => b.Id == residency.SourceBookingId.Value);
                        if (bookingReq != null && bookingReq.Status != "Completed")
                        {
                            bookingReq.Status = "Completed";
                            bookingReq.ProcessedTime = now;
                        }
                    }
                }

                _context.AuditLogs.Add(new AuditLog
                {
                    UserId = guardUserId,
                    ActionType = hasDamageOrMissing ? "CHECKIN_DRAW_UP_RECORD" : "CHECKIN_HANDOVER",
                    TargetEntity = "Handover",
                    TargetId = residency.Id.ToString(),
                    OldValue = null,
                    NewValue = hasDamageOrMissing
                        ? $"Lập biên bản Check-in cho SV ID={dto.StudentId} (Có thiết bị hỏng/thiếu -> Chờ Manager xử lý sự cố)"
                        : $"Hoàn tất Check-in & kích hoạt giường Occupied cho SV ID={dto.StudentId} (Chờ Manager hậu kiểm Check record)",
                    CreatedAt = now
                });

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var saved = await GetCheckinByIdAsync(handover.Id);

                if (_inAppNotificationService != null)
                {
                    if (hasDamageOrMissing)
                    {
                        await _inAppNotificationService.NotifyUserAsync(
                            dto.StudentId,
                            "Biên bản Check-in đang chờ Quản lý xử lý thiết bị",
                            $"Biên bản phòng {saved?.RoomNumber} - Giường {bed.BedCode} có ghi nhận thiết bị hư hỏng/thiếu, đang chờ Ban Quản lý khắc phục (troubleshooting) trước khi Bảo vệ xác nhận hoàn tất Check-in.",
                            "Checkin",
                            handover.Id.ToString());
                    }
                    else
                    {
                        await _inAppNotificationService.NotifyUserAsync(
                            dto.StudentId,
                            "Hoàn tất biên bản Check-in & kích hoạt giường ở",
                            $"Bảo vệ đã lập và hoàn tất biên bản bàn giao phòng {saved?.RoomNumber} - Giường {bed.BedCode} (Bed -> Occupied). Bạn có thể xem hoặc in biên bản tại mục Biên bản nhận phòng.",
                            "Checkin",
                            handover.Id.ToString());
                    }
                }

                return saved!;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        private async Task EnsureManagerExistsAsync(int managerUserId)
        {
            var exists = await _context.Managers.AnyAsync(m => m.UserId == managerUserId);
            if (!exists)
            {
                _context.Managers.Add(new Manager { UserId = managerUserId });
                await _context.SaveChangesAsync();
            }
        }

        public async Task<CheckinHandoverResponseDto> TroubleshootByManagerAsync(
            int managerUserId,
            int handoverId,
            ManagerTroubleshootCheckinDto? dto = null)
        {
            var handover = await _context.Handovers
                .Include(h => h.Residency)
                .Include(h => h.Room)
                .Include(h => h.HandoverItems).ThenInclude(hi => hi.Equipment)
                .FirstOrDefaultAsync(h => h.Id == handoverId && h.HandoverType == "Checkin");

            if (handover == null)
            {
                throw new ArgumentException("Không tìm thấy biên bản bàn giao Check-in.");
            }

            if (handover.Status != "Draft")
            {
                throw new InvalidOperationException("Biên bản này không ở trạng thái chờ Quản lý xử lý sự cố (Draft).");
            }

            var now = DateTime.UtcNow.AddHours(7);
            await EnsureManagerExistsAsync(managerUserId);

            // Nếu Manager chọn sửa chữa/thay thế luôn các thiết bị hỏng/thiếu trước khi bàn giao lại cho SV
            if (dto?.RepairDamagedEquipments != false)
            {
                foreach (var item in handover.HandoverItems)
                {
                    if (item.ItemCondition != "Good")
                    {
                        item.Note = string.IsNullOrWhiteSpace(item.Note)
                            ? "Quản lý đã khắc phục/thay thế thiết bị"
                            : $"{item.Note} (QL đã khắc phục)";
                        item.ItemCondition = "Good";
                    }
                    if (item.Equipment != null && item.Equipment.Condition != "Good")
                    {
                        item.Equipment.Condition = "Good";
                    }
                }
            }

            handover.Status = "Pending Acceptance";
            handover.ManagerReviewedById = managerUserId;
            handover.ManagerReviewedTime = null; // Để dành cho bước cuối "Check record" sau khi lưu biên bản
            handover.ManagerNote = string.IsNullOrWhiteSpace(dto?.Note)
                ? "Quản lý đã xử lý sự cố thiết bị (Troubleshooting) và chuyển Bảo vệ xác nhận Check-in."
                : dto!.Note.Trim();

            _context.AuditLogs.Add(new AuditLog
            {
                UserId = managerUserId,
                ActionType = "CHECKIN_TROUBLESHOOTING",
                TargetEntity = "Handover",
                TargetId = handover.Id.ToString(),
                OldValue = "Draft",
                NewValue = $"Manager xử lý sự cố thiết bị (Troubleshooting): {handover.ManagerNote}",
                CreatedAt = now
            });

            await _context.SaveChangesAsync();

            var saved = await GetCheckinByIdAsync(handover.Id);
            if (_inAppNotificationService != null)
            {
                await _inAppNotificationService.NotifyUserAsync(
                    handover.GuardId,
                    "Quản lý đã xử lý thiết bị - Mời xác nhận Check-in",
                    $"Ban Quản lý đã xử lý sự cố thiết bị phòng {saved?.RoomNumber} - Giường {saved?.BedCode} cho SV {saved?.StudentName} ({saved?.StudentCode}). Vui lòng bấm Confirm Check-in để chốt nhận phòng.",
                    "Checkin",
                    handover.Id.ToString());

                if (handover.Residency != null)
                {
                    await _inAppNotificationService.NotifyUserAsync(
                        handover.Residency.StudentId,
                        "Quản lý đã xử lý thiết bị - Chờ Bảo vệ xác nhận Check-in",
                        $"Ban Quản lý đã xử lý sự cố thiết bị phòng {saved?.RoomNumber} - Giường {saved?.BedCode}. Biên bản đã được chuyển cho Bảo vệ xác nhận hoàn tất Check-in.",
                        "Checkin",
                        handover.Id.ToString());
                }
            }

            return saved!;
        }

        public async Task<CheckinHandoverResponseDto> GuardConfirmCheckinAsync(int guardUserId, int handoverId)
        {
            var handover = await _context.Handovers
                .Include(h => h.Residency).ThenInclude(r => r!.Bed)
                .Include(h => h.Room)
                .FirstOrDefaultAsync(h => h.Id == handoverId && h.HandoverType == "Checkin");

            if (handover == null)
            {
                throw new ArgumentException("Không tìm thấy biên bản bàn giao Check-in.");
            }

            if (handover.Status == "Draft")
            {
                throw new InvalidOperationException("Biên bản có thiết bị hư hỏng/thiếu đang chờ Ban Quản lý xử lý sự cố (Troubleshooting). Không thể chốt Check-in.");
            }

            if (handover.Status == "Completed")
            {
                return (await GetCheckinByIdAsync(handover.Id))!;
            }

            var now = DateTime.UtcNow.AddHours(7);
            var today = DateOnly.FromDateTime(now);

            // Bước System: Save record & Bed -- Occupied
            handover.Status = "Completed";
            handover.CompletedTime = now;
            handover.StudentAcceptedFlag = true;
            handover.StudentAcceptedTime ??= now;

            if (handover.Residency != null)
            {
                handover.Residency.Status = "Active";
                handover.Residency.CheckInDate ??= today;

                var bed = handover.Residency.Bed
                    ?? await _context.Beds.FirstOrDefaultAsync(b => b.Id == handover.Residency.BedId);
                if (bed != null)
                {
                    bed.Status = "Occupied";
                }

                if (handover.Residency.SourceBookingId.HasValue)
                {
                    var bookingReq = await _context.BookingRequests
                        .FirstOrDefaultAsync(b => b.Id == handover.Residency.SourceBookingId.Value);
                    if (bookingReq != null && bookingReq.Status != "Completed")
                    {
                        bookingReq.Status = "Completed";
                        bookingReq.ProcessedTime = now;
                    }
                }
            }

            _context.AuditLogs.Add(new AuditLog
            {
                UserId = guardUserId,
                ActionType = "CHECKIN_GUARD_CONFIRM",
                TargetEntity = "Handover",
                TargetId = handover.Id.ToString(),
                OldValue = "Pending Acceptance",
                NewValue = $"Bảo vệ đã xác nhận hoàn tất Check-in (Save record & Bed -> Occupied) cho biên bản #{handover.Id}",
                CreatedAt = now
            });

            await _context.SaveChangesAsync();

            var saved = await GetCheckinByIdAsync(handover.Id);
            if (_inAppNotificationService != null && handover.Residency != null)
            {
                await _inAppNotificationService.NotifyUserAsync(
                    handover.Residency.StudentId,
                    "Đã lưu biên bản Check-in & kích hoạt giường ở",
                    $"Bảo vệ đã xác nhận Check-in phòng {saved?.RoomNumber} - Giường {saved?.BedCode} (Bed -> Occupied). Biên bản đã chuyển cho Ban Quản lý kiểm tra hồ sơ (Check record).",
                    "Checkin",
                    handover.Id.ToString());
            }

            return saved!;
        }

        public async Task<CheckinHandoverResponseDto> ManagerCheckRecordAsync(
            int managerUserId,
            int handoverId,
            ManagerCheckRecordDto? dto = null)
        {
            var handover = await _context.Handovers
                .Include(h => h.Residency)
                .Include(h => h.Room)
                .FirstOrDefaultAsync(h => h.Id == handoverId && h.HandoverType == "Checkin");

            if (handover == null)
            {
                throw new ArgumentException("Không tìm thấy biên bản bàn giao Check-in.");
            }

            if (handover.Status != "Completed")
            {
                throw new InvalidOperationException("Chỉ có thể kiểm tra chốt hồ sơ (Check record) sau khi Bảo vệ đã xác nhận hoàn tất Check-in.");
            }

            var now = DateTime.UtcNow.AddHours(7);
            await EnsureManagerExistsAsync(managerUserId);

            handover.ManagerReviewedById = managerUserId;
            handover.ManagerReviewedTime = now;
            if (!string.IsNullOrWhiteSpace(dto?.Note))
            {
                handover.ManagerNote = string.IsNullOrWhiteSpace(handover.ManagerNote)
                    ? dto.Note.Trim()
                    : $"{handover.ManagerNote} | Kiểm tra hồ sơ: {dto.Note.Trim()}";
            }
            else if (string.IsNullOrWhiteSpace(handover.ManagerNote))
            {
                handover.ManagerNote = "Ban Quản lý đã kiểm tra và duyệt lưu hồ sơ biên bản Check-in.";
            }

            _context.AuditLogs.Add(new AuditLog
            {
                UserId = managerUserId,
                ActionType = "CHECKIN_MANAGER_CHECK_RECORD",
                TargetEntity = "Handover",
                TargetId = handover.Id.ToString(),
                OldValue = "Completed",
                NewValue = $"Ban Quản lý đã hoàn tất kiểm tra biên bản Check-in (Check record) #{handover.Id}",
                CreatedAt = now
            });

            await _context.SaveChangesAsync();

            var saved = await GetCheckinByIdAsync(handover.Id);
            if (_inAppNotificationService != null && handover.Residency != null)
            {
                await _inAppNotificationService.NotifyUserAsync(
                    handover.Residency.StudentId,
                    "Ban Quản lý đã hậu kiểm biên bản Check-in",
                    $"Biên bản nhận phòng {saved?.RoomNumber} - Giường {saved?.BedCode} đã được Ban Quản lý kiểm tra và xác nhận hoàn tất.",
                    "Checkin",
                    handover.Id.ToString());
            }

            return saved!;
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
                .Include(h => h.ManagerReviewedBy).ThenInclude(m => m!.User)
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
                .Include(h => h.ManagerReviewedBy).ThenInclude(m => m!.User)
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
                .Include(h => h.ManagerReviewedBy).ThenInclude(m => m!.User)
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

        private static string ResolveWorkflowStage(Handover h)
        {
            if (h.Status == "Draft")
            {
                return "PendingManagerTroubleshooting";
            }
            if (h.Status == "Pending Acceptance")
            {
                return "PendingGuardConfirm";
            }
            // Status == "Completed"
            return h.ManagerReviewedTime.HasValue ? "Completed" : "PendingManagerCheckRecord";
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
            var managerUser = h.ManagerReviewedBy?.User;
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
                WorkflowStage = ResolveWorkflowStage(h),
                HasDamageOrMissingFlag = h.HasDamageOrMissingFlag,
                StudentAcceptedFlag = h.StudentAcceptedFlag,
                StudentAcceptedTime = h.StudentAcceptedTime,
                ManagerReviewedById = h.ManagerReviewedById,
                ManagerReviewedByName = managerUser?.FullName,
                ManagerReviewedTime = h.ManagerReviewedTime,
                ManagerNote = h.ManagerNote,
                OtherItems = meta?.OtherItems,
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
