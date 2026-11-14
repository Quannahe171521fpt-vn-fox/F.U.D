using FUD.DTOs.Utilities;
using FUD.Models;
using FUD.Repositories.Interfaces.Utilities;
using Microsoft.EntityFrameworkCore;

namespace FUD.Repositories.Utilities
{
    public class EwRepository : IEwRepository
    {
        private readonly FudContext _ctx;

        public EwRepository(FudContext ctx)
        {
            _ctx = ctx;
        }

        public async Task<IEnumerable<object>> GetGroupedUsagesAsync(string? blockCode, string? meterType, int? month, int? year, int? termId = null, int? billingPeriodId = null)
        {
            var query = from mr in _ctx.MeterReadings
                        join r in _ctx.Rooms on mr.RoomId equals r.Id
                        join b in _ctx.Blocks on r.BlockId equals b.Id
                        join bp in _ctx.BillingPeriods on mr.BillingPeriodId equals bp.Id
                        join t in _ctx.Terms on bp.TermId equals t.Id
                        join u in _ctx.Users on mr.RecordedById equals u.Id into uGroup
                        from u in uGroup.DefaultIfEmpty()
                        select new {
                            mr.Id,
                            mr.RoomId,
                            mr.MeterType,
                            mr.PreviousValue,
                            mr.CurrentValue,
                            Consumption = (decimal?)(mr.CurrentValue - mr.PreviousValue),
                            mr.Source,
                            mr.Status,
                            mr.Note,
                            mr.RecordedTime,
                            RecordedByName = u != null ? u.FullName : "Bảo vệ",
                            r.RoomNumber,
                            r.FloorLevel,
                            BlockId = b.Id,
                            BlockCode = b.Code,
                            BlockName = b.Name,
                            TermId = t.Id,
                            BillingPeriodId = bp.Id,
                            PeriodStatus = bp.Status,
                            bp.TargetMonth,
                            bp.TargetYear,
                            TermName = t.Name + " - " + t.AcademicYear
                        };

            if (!string.IsNullOrEmpty(blockCode))
            {
                var clean = blockCode.Trim().ToUpper();
                query = query.Where(x => x.BlockCode.ToUpper() == clean ||
                                         x.RoomNumber.ToUpper().Contains(clean));
            }

            if (!string.IsNullOrEmpty(meterType))
                query = query.Where(x => x.MeterType == meterType);

            if (termId.HasValue && termId.Value > 0)
                query = query.Where(x => x.TermId == termId.Value);

            if (billingPeriodId.HasValue && billingPeriodId.Value > 0)
                query = query.Where(x => x.BillingPeriodId == billingPeriodId.Value);

            if (month.HasValue)
                query = query.Where(x => x.TargetMonth == month.Value);

            if (year.HasValue)
                query = query.Where(x => x.TargetYear == year.Value);

            var rawData = await query.ToListAsync();

            var grouped = rawData
                .GroupBy(x => {
                    var roomNum = x.RoomNumber ?? string.Empty;
                    var baseRoom = roomNum.Length > 1 && (roomNum.EndsWith("L", StringComparison.OrdinalIgnoreCase) || roomNum.EndsWith("R", StringComparison.OrdinalIgnoreCase))
                        ? roomNum.Substring(0, roomNum.Length - 1)
                        : roomNum;

                    string displayBlock;
                    if (x.BlockCode.EndsWith(baseRoom, StringComparison.OrdinalIgnoreCase) || x.BlockCode.Contains("-" + baseRoom))
                    {
                        displayBlock = x.BlockCode;
                    }
                    else if (baseRoom.StartsWith(x.BlockCode, StringComparison.OrdinalIgnoreCase))
                    {
                        displayBlock = baseRoom;
                    }
                    else
                    {
                        displayBlock = $"{x.BlockCode}-{baseRoom}";
                    }

                    return (
                        BlockId: x.BlockId,
                        BlockCode: x.BlockCode,
                        BlockName: x.BlockName,
                        BaseRoom: baseRoom,
                        DisplayBlock: displayBlock,
                        MeterType: x.MeterType,
                        TermId: x.TermId,
                        BillingPeriodId: x.BillingPeriodId,
                        TargetMonth: x.TargetMonth,
                        TargetYear: x.TargetYear,
                        PeriodStatus: x.PeriodStatus,
                        TermName: x.TermName
                    );
                })
                .Select(g => {
                    var maxDate = g.Max(x => x.RecordedTime);
                    var readings = g.Select(x => new {
                        ReadingId = x.Id,
                        RoomId = x.RoomId,
                        RoomNumber = x.RoomNumber,
                        FloorLevel = x.FloorLevel,
                        MeterType = x.MeterType,
                        PreviousValue = x.PreviousValue,
                        CurrentValue = x.CurrentValue,
                        Consumption = x.Consumption ?? (x.CurrentValue - x.PreviousValue),
                        Source = x.Source,
                        Status = x.Status,
                        Note = x.Note,
                        RecordedTime = x.RecordedTime,
                        RecordedByName = x.RecordedByName
                    }).ToList();

                    var roomL = readings.FirstOrDefault(r => r.RoomNumber.EndsWith("L", StringComparison.OrdinalIgnoreCase)) ?? readings.FirstOrDefault();
                    var roomR = readings.FirstOrDefault(r => r.RoomNumber.EndsWith("R", StringComparison.OrdinalIgnoreCase)) ?? (readings.Count > 1 ? readings.Last() : null);

                    return new {
                        Id = g.First().Id,
                        Block = g.Key.DisplayBlock,
                        TenBlock = g.Key.DisplayBlock,
                        BlockCode = g.Key.BlockCode,
                        BlockName = g.Key.BlockName,
                        RoomNumber = g.Key.BaseRoom,
                        TermId = g.Key.TermId,
                        BillingPeriodId = g.Key.BillingPeriodId,
                        PeriodStatus = g.Key.PeriodStatus,
                        TargetMonth = g.Key.TargetMonth,
                        TargetYear = g.Key.TargetYear,
                        LoaiSuDung = g.Key.MeterType,
                        NgayTao = (DateTime?)maxDate,
                        HocKy = g.Key.TermName,
                        MeterNumberDisplay = $"L: {roomL?.CurrentValue.ToString() ?? "0"} | R: {roomR?.CurrentValue.ToString() ?? "0"}",
                        TotalConsumption = readings.Sum(x => x.Consumption),
                        DonVi = g.Key.MeterType == "Electricity" ? "kWh" : "m3",
                        Readings = readings
                    };
                })
                .OrderBy(x => x.Block)
                .ThenBy(x => x.TargetYear)
                .ThenBy(x => x.TargetMonth)
                .ThenBy(x => x.LoaiSuDung)
                .Cast<object>()
                .ToList();

            return grouped;
        }

        public async Task<List<BillingPeriod>> GetAllBillingPeriodsAsync()
        {
            return await _ctx.BillingPeriods
                .Include(bp => bp.Term)
                .OrderByDescending(bp => bp.TargetYear)
                .ThenByDescending(bp => bp.TargetMonth)
                .ToListAsync();
        }

        public async Task<List<BillingPeriod>> GetBillingPeriodsAsync() => await GetAllBillingPeriodsAsync();

        public async Task<BillingPeriod?> GetBillingPeriodByIdAsync(int id)
        {
            return await _ctx.BillingPeriods
                .Include(bp => bp.Term)
                .FirstOrDefaultAsync(bp => bp.Id == id);
        }

        public async Task<List<Block>> GetAllBlocksAsync()
        {
            return await _ctx.Blocks
                .Include(b => b.Dorm)
                .Where(x => x.Status == "Active")
                .ToListAsync();
        }

        public async Task<Block?> GetBlockByCodeAsync(string blockCode, int dormId)
        {
            return await _ctx.Blocks.FirstOrDefaultAsync(x => x.Code == blockCode && x.DormId == dormId && x.Status == "Active");
        }

        public async Task<List<Room>> GetAllActiveRoomsWithBlockAsync()
        {
            return await _ctx.Rooms
                .Include(r => r.Block)
                    .ThenInclude(b => b.Dorm)
                .Where(r => r.Status != "Inactive")
                .OrderBy(r => r.Block.Code)
                .ThenBy(r => r.RoomNumber)
                .ToListAsync();
        }

        /// <summary>
        /// Tìm cặp phòng L/R theo block code (ví dụ "A101").
        /// Hỗ trợ 2 cách lưu DB:
        ///   - Block.Code="A101", RoomNumber="A101L"/"A101R"
        ///   - Block.Code="A1",   RoomNumber="01L"/"01R"
        ///   - Block.Code="A101", RoomNumber="L"/"R" (edge case)
        /// </summary>
        public async Task<List<Room>> GetRoomsByBlockCodeAsync(string blockCode)
        {
            if (string.IsNullOrWhiteSpace(blockCode))
                return new List<Room>();

            var clean = blockCode.Trim().ToUpper();

            var allRooms = await _ctx.Rooms
                .Include(r => r.Block)
                    .ThenInclude(b => b.Dorm)
                .Where(r => r.Status != "Inactive")
                .ToListAsync();

            // Khớp trực tiếp mã Block trong DB ("A1", "A2", "B1", "B2") hoặc mã Block+Room
            var matched = allRooms.Where(r =>
            {
                var blockC = r.Block?.Code?.ToUpper() ?? string.Empty;
                var roomN  = r.RoomNumber?.ToUpper() ?? string.Empty;

                // 1. Nếu mã truyền vào đúng là mã Block trong DB (A1, A2, B1, B2)
                if (blockC == clean) return true;

                // 2. Tính fullCode = BlockCode + RoomNumber (bỏ hậu tố L/R)
                var baseRoomN = roomN.EndsWith("L") || roomN.EndsWith("R")
                    ? roomN.Substring(0, roomN.Length - 1)
                    : roomN;

                var fullCode = baseRoomN.StartsWith(blockC) ? baseRoomN : (blockC + baseRoomN);

                // So khớp với clean hoặc các biến thể
                if (fullCode == clean) return true;

                // Trường hợp RoomNumber đã là "A101L"/"A101R"
                var fullWithSuffix = roomN.StartsWith(blockC) ? roomN : (blockC + roomN);
                if (fullWithSuffix == clean + "L" || fullWithSuffix == clean + "R") return true;
                if (roomN == clean + "L" || roomN == clean + "R") return true;
                if (roomN == clean) return true;

                return false;
            }).ToList();

            return matched;
        }

        public async Task<List<MeterReading>> GetReadingsByPeriodAsync(int billingPeriodId)
        {
            return await _ctx.MeterReadings
                .Include(mr => mr.Room)
                    .ThenInclude(r => r.Block)
                .Where(mr => mr.BillingPeriodId == billingPeriodId && mr.CheckoutRequestId == null && mr.TransferRequestId == null)
                .ToListAsync();
        }

        public async Task<MeterReading?> GetReadingForPeriodAsync(int billingPeriodId, int roomId, string meterType)
        {
            return await _ctx.MeterReadings
                .FirstOrDefaultAsync(mr => mr.BillingPeriodId == billingPeriodId &&
                                           mr.RoomId == roomId &&
                                           mr.MeterType == meterType &&
                                           mr.CheckoutRequestId == null &&
                                           mr.TransferRequestId == null);
        }

        public async Task<MeterReading?> GetLatestReadingBeforePeriodAsync(int roomId, string meterType, int targetMonth, int targetYear)
        {
            var prevReading = await _ctx.MeterReadings
                .Include(mr => mr.BillingPeriod)
                .Where(mr => mr.RoomId == roomId &&
                             mr.MeterType == meterType &&
                             mr.BillingPeriodId != null &&
                             mr.BillingPeriod != null &&
                             (mr.BillingPeriod.TargetYear < targetYear ||
                             (mr.BillingPeriod.TargetYear == targetYear && mr.BillingPeriod.TargetMonth < targetMonth)))
                .OrderByDescending(mr => mr.BillingPeriod!.TargetYear)
                .ThenByDescending(mr => mr.BillingPeriod!.TargetMonth)
                .ThenByDescending(mr => mr.RecordedTime)
                .FirstOrDefaultAsync();

            if (prevReading != null)
                return prevReading;

            return await _ctx.MeterReadings
                .Where(mr => mr.RoomId == roomId && mr.MeterType == meterType)
                .OrderByDescending(mr => mr.RecordedTime)
                .FirstOrDefaultAsync();
        }

        public async Task<decimal> GetLatestReadingValueAsync(int roomId, string meterType, int currentBillingPeriodId)
        {
            var lastReading = await _ctx.MeterReadings
                .Where(mr => mr.RoomId == roomId && mr.MeterType == meterType && mr.BillingPeriodId < currentBillingPeriodId)
                .OrderByDescending(mr => mr.BillingPeriodId)
                .FirstOrDefaultAsync();

            return lastReading?.CurrentValue ?? 0;
        }

        public async Task AddMeterReadingAsync(MeterReading reading)
        {
            await _ctx.MeterReadings.AddAsync(reading);
        }

        public async Task<MeterReading> AddReadingAsync(MeterReading reading)
        {
            await _ctx.MeterReadings.AddAsync(reading);
            await _ctx.SaveChangesAsync();
            return reading;
        }

        public async Task<MeterReading?> GetReadingByIdAsync(int id)
        {
            return await _ctx.MeterReadings
                .Include(mr => mr.Room)
                    .ThenInclude(r => r.Block)
                .FirstOrDefaultAsync(mr => mr.Id == id);
        }

        public async Task<MeterReading?> GetByIdAsync(int id) => await GetReadingByIdAsync(id);

        public async Task UpdateReadingAsync(MeterReading reading)
        {
            _ctx.MeterReadings.Update(reading);
            await _ctx.SaveChangesAsync();
        }

        public void DeleteReading(MeterReading reading)
        {
            _ctx.MeterReadings.Remove(reading);
        }

        // ── Import Batches ──────────────────────────────────────
        public async Task AddImportBatchAsync(ImportBatch batch)
        {
            await _ctx.ImportBatches.AddAsync(batch);
            await _ctx.SaveChangesAsync();
        }

        public async Task<List<ImportBatch>> GetImportBatchesAsync(int? billingPeriodId)
        {
            var query = _ctx.ImportBatches
                .Include(b => b.BillingPeriod)
                .Include(b => b.Block)
                .Include(b => b.Guard)
                    .ThenInclude(g => g.User)
                .AsQueryable();

            if (billingPeriodId.HasValue)
                query = query.Where(b => b.BillingPeriodId == billingPeriodId.Value);

            return await query.OrderByDescending(b => b.ImportTime).ToListAsync();
        }

        public async Task<ImportBatch?> GetImportBatchByIdAsync(int id)
        {
            return await _ctx.ImportBatches
                .Include(b => b.BillingPeriod)
                .Include(b => b.Block)
                .Include(b => b.Guard)
                    .ThenInclude(g => g.User)
                .FirstOrDefaultAsync(b => b.Id == id);
        }

        // ── Anomalies & Recheck ─────────────────────────────────
        public async Task AddUsageAnomalyAsync(UsageAnomaly anomaly)
        {
            await _ctx.UsageAnomalies.AddAsync(anomaly);
        }

        public async Task AddAnomalyAsync(UsageAnomaly anomaly)
        {
            await _ctx.UsageAnomalies.AddAsync(anomaly);
            await _ctx.SaveChangesAsync();
        }

        public async Task<List<UsageAnomaly>> GetAnomaliesAsync(int? billingPeriodId, string? status)
        {
            var query = _ctx.UsageAnomalies
                .Include(ua => ua.Reading)
                    .ThenInclude(r => r.Room)
                        .ThenInclude(rm => rm.Block)
                .Include(ua => ua.Reading)
                    .ThenInclude(r => r.BillingPeriod)
                        .ThenInclude(bp => bp!.Term)
                .Include(ua => ua.InspectionRequests)
                .AsQueryable();

            if (billingPeriodId.HasValue)
                query = query.Where(ua => ua.Reading.BillingPeriodId == billingPeriodId.Value);

            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(ua => ua.Status == status);

            return await query
                .OrderByDescending(ua => ua.CreatedTime)
                .ToListAsync();
        }

        public async Task<UsageAnomaly?> GetAnomalyByIdAsync(int id)
        {
            return await _ctx.UsageAnomalies
                .Include(ua => ua.Reading)
                    .ThenInclude(r => r.Room)
                        .ThenInclude(rm => rm.Block)
                .Include(ua => ua.Reading)
                    .ThenInclude(r => r.BillingPeriod)
                .Include(ua => ua.InspectionRequests)
                .FirstOrDefaultAsync(ua => ua.Id == id);
        }

        public async Task AddInspectionRequestAsync(InspectionRequest request)
        {
            await _ctx.InspectionRequests.AddAsync(request);
        }

        public async Task<List<InspectionRequest>> GetRecheckRequestsAsync(int? guardId, string? status)
        {
            var query = _ctx.InspectionRequests
                .Include(ir => ir.Anomaly)
                    .ThenInclude(a => a.Reading)
                        .ThenInclude(r => r.Room)
                            .ThenInclude(rm => rm.Block)
                .Include(ir => ir.Anomaly)
                    .ThenInclude(a => a.Reading)
                        .ThenInclude(r => r.BillingPeriod)
                            .ThenInclude(bp => bp!.Term)
                .Include(ir => ir.AssignedGuard)
                    .ThenInclude(g => g.User)
                .AsQueryable();

            if (guardId.HasValue)
                query = query.Where(ir => ir.AssignedGuardId == guardId.Value);

            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(ir => ir.Status == status);

            return await query
                .OrderByDescending(ir => ir.CreatedTime)
                .ToListAsync();
        }

        public async Task<List<InspectionRequest>> GetInspectionRequestsByGuardAsync(int guardId, string? status)
            => await GetRecheckRequestsAsync(guardId, status);

        public async Task<InspectionRequest?> GetRecheckRequestByIdAsync(int id)
        {
            return await _ctx.InspectionRequests
                .Include(ir => ir.Anomaly)
                    .ThenInclude(a => a.Reading)
                        .ThenInclude(r => r.Room)
                .FirstOrDefaultAsync(ir => ir.Id == id);
        }

        public async Task<InspectionRequest?> GetInspectionRequestByIdAsync(int id)
            => await GetRecheckRequestByIdAsync(id);

        public async Task UpdateInspectionRequestAsync(InspectionRequest request)
        {
            _ctx.InspectionRequests.Update(request);
            await _ctx.SaveChangesAsync();
        }

        public async Task SaveChangesAsync()
        {
            await _ctx.SaveChangesAsync();
        }
    }
}
