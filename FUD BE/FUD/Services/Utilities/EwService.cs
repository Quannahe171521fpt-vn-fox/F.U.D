using FUD.DTOs.Utilities;
using FUD.Infrastructure.Excel;
using FUD.Models;
using FUD.Repositories.Interfaces.Utilities;
using FUD.Services.Interfaces;
using FUD.Services.Interfaces.Utilities;

namespace FUD.Services.Utilities
{
    public class EwService : IEwService
    {
        private readonly IEwRepository _ewRepo;
        private readonly IInAppNotificationService? _inAppNotificationService;

        public EwService(IEwRepository ewRepo, IInAppNotificationService? inAppNotificationService = null)
        {
            _ewRepo = ewRepo;
            _inAppNotificationService = inAppNotificationService;
        }

        public async Task<IEnumerable<object>> GetUsagesAsync(
            string? blockCode,
            string? meterType,
            int? month,
            int? year,
            int? termId = null,
            int? billingPeriodId = null,
            int? dormId = null,
            int? userId = null,
            bool isManager = false)
        {
            if (!isManager && userId.HasValue)
            {
                var guardDormId = await _ewRepo.GetGuardDormIdAsync(userId.Value);
                if (guardDormId.HasValue)
                {
                    dormId = guardDormId.Value;
                }
            }

            return await _ewRepo.GetGroupedUsagesAsync(blockCode, meterType, month, year, termId, billingPeriodId, dormId);
        }

        // ── L1: Lookup Billing Periods ──────────────────────────
        public async Task<List<BillingPeriodLookupDto>> GetBillingPeriodsAsync()
        {
            var periods = await _ewRepo.GetAllBillingPeriodsAsync();
            return periods.Select(bp => new BillingPeriodLookupDto
            {
                Id = bp.Id,
                TermId = bp.TermId,
                TargetMonth = bp.TargetMonth,
                TargetYear = bp.TargetYear,
                DisplayName = $"Tháng {bp.TargetMonth}/{bp.TargetYear}",
                Status = bp.Status,
                TermName = bp.Term != null ? $"{bp.Term.Name} ({bp.Term.AcademicYear})" : string.Empty
            }).ToList();
        }

        // ── L2: Block Progress trong kỳ ─────────────────────────
        public async Task<BlockProgressResponseDto> GetBlockProgressAsync(int billingPeriodId, int? userId = null, bool isManager = false)
        {
            var period = await _ewRepo.GetBillingPeriodByIdAsync(billingPeriodId)
                ?? throw new KeyNotFoundException($"Không tìm thấy kỳ thanh toán id = {billingPeriodId}");

            var allRooms = await _ewRepo.GetAllActiveRoomsWithBlockAsync();
            var readings = await _ewRepo.GetReadingsByPeriodAsync(billingPeriodId);

            if (!isManager && userId.HasValue)
            {
                var guardDormId = await _ewRepo.GetGuardDormIdAsync(userId.Value);
                if (guardDormId.HasValue)
                {
                    allRooms = allRooms.Where(r => r.Block.DormId == guardDormId.Value).ToList();
                }
            }

            var blockGroups = allRooms
                .GroupBy(r => new
                {
                    BlockId = r.BlockId,
                    BlockCode = r.Block.Code,
                    BlockName = r.Block.Name,
                    DormName = r.Block.Dorm?.Name ?? string.Empty,
                    BaseRoom = GetBaseRoomCode(r.RoomNumber)
                })
                .ToList();

            var blockItems = new List<BlockProgressItemDto>();
            int completedCount = 0;

            foreach (var bg in blockGroups)
            {
                var roomL = bg.FirstOrDefault(r => r.RoomNumber.EndsWith("L", StringComparison.OrdinalIgnoreCase)) ?? bg.FirstOrDefault();
                var roomR = bg.FirstOrDefault(r => r.RoomNumber.EndsWith("R", StringComparison.OrdinalIgnoreCase)) ?? (bg.Count() > 1 ? bg.Last() : null);

                RoomReadingStatusDto? elecL = null, elecR = null, waterL = null, waterR = null;

                if (roomL != null)
                {
                    var rElec = readings.FirstOrDefault(x => x.RoomId == roomL.Id && x.MeterType == "Electricity");
                    elecL = new RoomReadingStatusDto
                    {
                        RoomId = roomL.Id,
                        RoomNumber = roomL.RoomNumber,
                        HasReading = rElec != null,
                        CurrentValue = rElec?.CurrentValue,
                        RecordedAt = rElec?.RecordedTime.ToString("dd/MM/yyyy"),
                        Status = rElec?.Status
                    };

                    var rWater = readings.FirstOrDefault(x => x.RoomId == roomL.Id && x.MeterType == "Water");
                    waterL = new RoomReadingStatusDto
                    {
                        RoomId = roomL.Id,
                        RoomNumber = roomL.RoomNumber,
                        HasReading = rWater != null,
                        CurrentValue = rWater?.CurrentValue,
                        RecordedAt = rWater?.RecordedTime.ToString("dd/MM/yyyy"),
                        Status = rWater?.Status
                    };
                }

                if (roomR != null && roomR.Id != roomL?.Id)
                {
                    var rElec = readings.FirstOrDefault(x => x.RoomId == roomR.Id && x.MeterType == "Electricity");
                    elecR = new RoomReadingStatusDto
                    {
                        RoomId = roomR.Id,
                        RoomNumber = roomR.RoomNumber,
                        HasReading = rElec != null,
                        CurrentValue = rElec?.CurrentValue,
                        RecordedAt = rElec?.RecordedTime.ToString("dd/MM/yyyy"),
                        Status = rElec?.Status
                    };

                    var rWater = readings.FirstOrDefault(x => x.RoomId == roomR.Id && x.MeterType == "Water");
                    waterR = new RoomReadingStatusDto
                    {
                        RoomId = roomR.Id,
                        RoomNumber = roomR.RoomNumber,
                        HasReading = rWater != null,
                        CurrentValue = rWater?.CurrentValue,
                        RecordedAt = rWater?.RecordedTime.ToString("dd/MM/yyyy"),
                        Status = rWater?.Status
                    };
                }

                bool isCompleted = (elecL?.HasReading == true && waterL?.HasReading == true) &&
                                   (roomR == null || roomR.Id == roomL?.Id || (elecR?.HasReading == true && waterR?.HasReading == true));

                if (isCompleted) completedCount++;

                blockItems.Add(new BlockProgressItemDto
                {
                    BlockCode = bg.Key.BaseRoom,
                    BlockName = bg.Key.BlockName,
                    DormName = bg.Key.DormName,
                    ElectricityLeft = elecL,
                    ElectricityRight = elecR,
                    WaterLeft = waterL,
                    WaterRight = waterR
                });
            }

            return new BlockProgressResponseDto
            {
                BillingPeriodId = period.Id,
                DisplayName = $"Tháng {period.TargetMonth}/{period.TargetYear}",
                TotalBlocks = blockItems.Count,
                CompletedBlocks = completedCount,
                MissingBlocks = blockItems.Count - completedCount,
                Blocks = blockItems
            };
        }

        // ── M1: Form Data cho 1 Block ────────────────────────────
        public async Task<BlockFormDataResponseDto> GetBlockFormDataAsync(string blockCode, int billingPeriodId, int? userId = null, bool isManager = false)
        {
            var period = await _ewRepo.GetBillingPeriodByIdAsync(billingPeriodId)
                ?? throw new KeyNotFoundException($"Không tìm thấy kỳ thanh toán id = {billingPeriodId}");

            var rooms = await _ewRepo.GetRoomsByBlockCodeAsync(blockCode);
            if (!rooms.Any())
                throw new KeyNotFoundException($"Không tìm thấy block/phòng có mã '{blockCode}'");

            if (!isManager && userId.HasValue)
            {
                var guard = await _ewRepo.GetGuardByUserIdAsync(userId.Value);
                if (guard == null)
                    throw new UnauthorizedAccessException("Không tìm thấy thông tin phân công bảo vệ.");

                var roomDifferentDorm = rooms.FirstOrDefault(r => r.Block.DormId != guard.DormId);
                if (roomDifferentDorm != null)
                {
                    var blockDormName = roomDifferentDorm.Block.Dorm?.Name ?? roomDifferentDorm.Block.DormId.ToString();
                    var guardDormName = guard.Dorm?.Name ?? guard.DormId.ToString();
                    throw new UnauthorizedAccessException($"Bạn không có quyền truy cập toà/phòng '{blockCode}' thuộc KTX {blockDormName}. Bạn chỉ được quản lý KTX {guardDormName}.");
                }
            }

            var roomL = rooms.FirstOrDefault(r => r.RoomNumber.EndsWith("L", StringComparison.OrdinalIgnoreCase)) ?? rooms.First();
            var roomR = rooms.FirstOrDefault(r => r.RoomNumber.EndsWith("R", StringComparison.OrdinalIgnoreCase)) ?? (rooms.Count > 1 ? rooms.Last() : null);

            var result = new BlockFormDataResponseDto
            {
                BlockCode = blockCode,
                BillingPeriodId = period.Id,
                DisplayPeriod = $"Tháng {period.TargetMonth}/{period.TargetYear}"
            };

            if (roomL != null)
            {
                var curElec = await _ewRepo.GetReadingForPeriodAsync(period.Id, roomL.Id, "Electricity");
                var oldElec = await _ewRepo.GetLatestReadingBeforePeriodAsync(roomL.Id, "Electricity", period.TargetMonth, period.TargetYear);
                result.ElectricityLeft = new RoomOldReadingDto
                {
                    RoomId = roomL.Id,
                    RoomNumber = roomL.RoomNumber,
                    OldReading = oldElec?.CurrentValue ?? 0,
                    AlreadyRecorded = curElec != null,
                    ExistingCurrentValue = curElec?.CurrentValue
                };

                var curWater = await _ewRepo.GetReadingForPeriodAsync(period.Id, roomL.Id, "Water");
                var oldWater = await _ewRepo.GetLatestReadingBeforePeriodAsync(roomL.Id, "Water", period.TargetMonth, period.TargetYear);
                result.WaterLeft = new RoomOldReadingDto
                {
                    RoomId = roomL.Id,
                    RoomNumber = roomL.RoomNumber,
                    OldReading = oldWater?.CurrentValue ?? 0,
                    AlreadyRecorded = curWater != null,
                    ExistingCurrentValue = curWater?.CurrentValue
                };
            }

            if (roomR != null && roomR.Id != roomL?.Id)
            {
                var curElec = await _ewRepo.GetReadingForPeriodAsync(period.Id, roomR.Id, "Electricity");
                var oldElec = await _ewRepo.GetLatestReadingBeforePeriodAsync(roomR.Id, "Electricity", period.TargetMonth, period.TargetYear);
                result.ElectricityRight = new RoomOldReadingDto
                {
                    RoomId = roomR.Id,
                    RoomNumber = roomR.RoomNumber,
                    OldReading = oldElec?.CurrentValue ?? 0,
                    AlreadyRecorded = curElec != null,
                    ExistingCurrentValue = curElec?.CurrentValue
                };

                var curWater = await _ewRepo.GetReadingForPeriodAsync(period.Id, roomR.Id, "Water");
                var oldWater = await _ewRepo.GetLatestReadingBeforePeriodAsync(roomR.Id, "Water", period.TargetMonth, period.TargetYear);
                result.WaterRight = new RoomOldReadingDto
                {
                    RoomId = roomR.Id,
                    RoomNumber = roomR.RoomNumber,
                    OldReading = oldWater?.CurrentValue ?? 0,
                    AlreadyRecorded = curWater != null,
                    ExistingCurrentValue = curWater?.CurrentValue
                };
            }

            return result;
        }

        // ── M2: Preview Manual Reading ──────────────────────────
        public async Task<ManualPreviewResponseDto> PreviewManualReadingAsync(ManualPreviewRequestDto request, int? userId = null, bool isManager = false)
        {
            var period = await _ewRepo.GetBillingPeriodByIdAsync(request.BillingPeriodId)
                ?? throw new KeyNotFoundException($"Không tìm thấy kỳ thanh toán id = {request.BillingPeriodId}");

            var rooms = await _ewRepo.GetRoomsByBlockCodeAsync(request.BlockCode);
            if (!rooms.Any())
                throw new KeyNotFoundException($"Không tìm thấy phòng tương ứng mã block '{request.BlockCode}'");

            if (!isManager && userId.HasValue)
            {
                var guard = await _ewRepo.GetGuardByUserIdAsync(userId.Value);
                if (guard == null)
                    throw new UnauthorizedAccessException("Không tìm thấy thông tin phân công bảo vệ.");

                var roomDifferentDorm = rooms.FirstOrDefault(r => r.Block.DormId != guard.DormId);
                if (roomDifferentDorm != null)
                {
                    var blockDormName = roomDifferentDorm.Block.Dorm?.Name ?? roomDifferentDorm.Block.DormId.ToString();
                    var guardDormName = guard.Dorm?.Name ?? guard.DormId.ToString();
                    throw new UnauthorizedAccessException($"Bạn không có quyền xem trước chỉ số cho toà/phòng '{request.BlockCode}' thuộc KTX {blockDormName}. Bạn chỉ được quản lý KTX {guardDormName}.");
                }
            }

            var roomL = rooms.FirstOrDefault(r => r.RoomNumber.EndsWith("L", StringComparison.OrdinalIgnoreCase)) ?? rooms.First();
            var roomR = rooms.FirstOrDefault(r => r.RoomNumber.EndsWith("R", StringComparison.OrdinalIgnoreCase)) ?? (rooms.Count > 1 ? rooms.Last() : null);

            var items = new List<ReadingPreviewItemDto>();

            if (request.ElectricityLeftNewValue.HasValue && roomL != null)
            {
                var oldReading = (await _ewRepo.GetLatestReadingBeforePeriodAsync(roomL.Id, "Electricity", period.TargetMonth, period.TargetYear))?.CurrentValue ?? 0;
                var prevTermCons = await _ewRepo.GetRoomTermConsumptionBeforePeriodAsync(roomL.Id, "Electricity", period.TermId, period.Id);
                items.Add(EvaluateReading(roomL.Id, roomL.RoomNumber, "Electricity", oldReading, request.ElectricityLeftNewValue.Value, prevTermCons));
            }

            if (request.ElectricityRightNewValue.HasValue && roomR != null && roomR.Id != roomL?.Id)
            {
                var oldReading = (await _ewRepo.GetLatestReadingBeforePeriodAsync(roomR.Id, "Electricity", period.TargetMonth, period.TargetYear))?.CurrentValue ?? 0;
                var prevTermCons = await _ewRepo.GetRoomTermConsumptionBeforePeriodAsync(roomR.Id, "Electricity", period.TermId, period.Id);
                items.Add(EvaluateReading(roomR.Id, roomR.RoomNumber, "Electricity", oldReading, request.ElectricityRightNewValue.Value, prevTermCons));
            }

            if (request.WaterLeftNewValue.HasValue && roomL != null)
            {
                var oldReading = (await _ewRepo.GetLatestReadingBeforePeriodAsync(roomL.Id, "Water", period.TargetMonth, period.TargetYear))?.CurrentValue ?? 0;
                var prevTermCons = await _ewRepo.GetRoomTermConsumptionBeforePeriodAsync(roomL.Id, "Water", period.TermId, period.Id);
                items.Add(EvaluateReading(roomL.Id, roomL.RoomNumber, "Water", oldReading, request.WaterLeftNewValue.Value, prevTermCons));
            }

            if (request.WaterRightNewValue.HasValue && roomR != null && roomR.Id != roomL?.Id)
            {
                var oldReading = (await _ewRepo.GetLatestReadingBeforePeriodAsync(roomR.Id, "Water", period.TargetMonth, period.TargetYear))?.CurrentValue ?? 0;
                var prevTermCons = await _ewRepo.GetRoomTermConsumptionBeforePeriodAsync(roomR.Id, "Water", period.TermId, period.Id);
                items.Add(EvaluateReading(roomR.Id, roomR.RoomNumber, "Water", oldReading, request.WaterRightNewValue.Value, prevTermCons));
            }

            return new ManualPreviewResponseDto
            {
                HasErrors = items.Any(x => x.Status == "Error"),
                Results = items
            };
        }

        // ── M3: Confirm Manual Reading ──────────────────────────
        public async Task<ManualConfirmResponseDto> ConfirmManualReadingAsync(int guardUserId, ManualConfirmRequestDto request, bool isManager = false)
        {
            var period = await _ewRepo.GetBillingPeriodByIdAsync(request.BillingPeriodId)
                ?? throw new KeyNotFoundException($"Không tìm thấy kỳ thanh toán id = {request.BillingPeriodId}");

            if (period.Status != "Open")
                throw new InvalidOperationException($"Kỳ thanh toán Tháng {period.TargetMonth}/{period.TargetYear} hiện không mở ({period.Status})");

            var rooms = await _ewRepo.GetRoomsByBlockCodeAsync(request.BlockCode);
            if (!rooms.Any())
                throw new KeyNotFoundException($"Không tìm thấy phòng tương ứng mã block '{request.BlockCode}'");

            if (!isManager)
            {
                var guard = await _ewRepo.GetGuardByUserIdAsync(guardUserId);
                if (guard == null)
                    throw new UnauthorizedAccessException("Tài khoản không phải bảo vệ hợp lệ hoặc chưa được gán Ký túc xá.");

                var roomDifferentDorm = rooms.FirstOrDefault(r => r.Block.DormId != guard.DormId);
                if (roomDifferentDorm != null)
                {
                    var blockDormName = roomDifferentDorm.Block.Dorm?.Name ?? roomDifferentDorm.Block.DormId.ToString();
                    var guardDormName = guard.Dorm?.Name ?? guard.DormId.ToString();
                    throw new UnauthorizedAccessException($"Bảo vệ không có quyền ghi nhận chỉ số cho toà/phòng '{request.BlockCode}' thuộc KTX {blockDormName}. Bạn chỉ được quản lý KTX {guardDormName}.");
                }
            }

            var roomL = rooms.FirstOrDefault(r => r.RoomNumber.EndsWith("L", StringComparison.OrdinalIgnoreCase)) ?? rooms.First();
            var roomR = rooms.FirstOrDefault(r => r.RoomNumber.EndsWith("R", StringComparison.OrdinalIgnoreCase)) ?? (rooms.Count > 1 ? rooms.Last() : null);

            var pairs = new List<(Room? Room, string MeterType, decimal? NewVal)>
            {
                (roomL, "Electricity", request.ElectricityLeftNewValue),
                (roomR, "Electricity", request.ElectricityRightNewValue),
                (roomL, "Water", request.WaterLeftNewValue),
                (roomR, "Water", request.WaterRightNewValue)
            };

            var response = new ManualConfirmResponseDto();
            var affectedRoomIds = new HashSet<int>();

            foreach (var (room, meterType, newVal) in pairs)
            {
                if (room == null || !newVal.HasValue) continue;

                if (newVal.Value < 0)
                {
                    if (request.ImportOnlyValid)
                    {
                        response.Skipped++;
                        response.Results.Add(new ReadingConfirmResultDto
                        {
                            RoomNumber = room.RoomNumber,
                            MeterType = meterType,
                            Status = "Skipped",
                            AnomalyReason = "Chỉ số không được âm"
                        });
                        continue;
                    }
                    else
                    {
                        throw new ArgumentException($"Chỉ số {meterType} của phòng {room.RoomNumber} không được âm");
                    }
                }

                var prevReading = await _ewRepo.GetLatestReadingBeforePeriodAsync(room.Id, meterType, period.TargetMonth, period.TargetYear);
                decimal oldVal = prevReading?.CurrentValue ?? 0;
                decimal prevTermCons = await _ewRepo.GetRoomTermConsumptionBeforePeriodAsync(room.Id, meterType, period.TermId, period.Id);

                var existing = await _ewRepo.GetReadingForPeriodAsync(period.Id, room.Id, meterType);
                bool isNew = (existing == null);
                var reading = existing ?? new MeterReading
                {
                    BillingPeriodId = period.Id,
                    RoomId = room.Id,
                    MeterType = meterType,
                    Source = "Manual"
                };

                reading.PreviousValue = oldVal;
                reading.CurrentValue = newVal.Value;
                reading.RecordedById = guardUserId;
                reading.RecordedTime = DateTime.UtcNow;
                reading.Note = request.Note;

                var (isFlagged, anomalyReason) = CheckReadingAnomaly(meterType, oldVal, newVal.Value, prevTermCons);

                reading.Status = isFlagged ? "Flagged" : "Recorded";

                if (isNew)
                {
                    await _ewRepo.AddMeterReadingAsync(reading);
                }

                await _ewRepo.SaveChangesAsync();
                affectedRoomIds.Add(room.Id);

                if (isFlagged && !string.IsNullOrEmpty(anomalyReason))
                {
                    var anomaly = new UsageAnomaly
                    {
                        ReadingId = reading.Id,
                        FlagReason = anomalyReason,
                        Status = "Flagged",
                        CreatedTime = DateTime.UtcNow
                    };
                    await _ewRepo.AddUsageAnomalyAsync(anomaly);
                    await _ewRepo.SaveChangesAsync();
                    response.Flagged++;
                    response.ManagerAlerted = true;
                }

                response.Saved++;
                response.Results.Add(new ReadingConfirmResultDto
                {
                    ReadingId = reading.Id,
                    RoomNumber = room.RoomNumber,
                    MeterType = meterType,
                    Status = reading.Status,
                    AnomalyReason = anomalyReason
                });
            }

            if (_inAppNotificationService != null && response.Flagged > 0)
            {
                await _inAppNotificationService.NotifyRolesAsync(
                    new[] { AccountConstants.Roles.Manager },
                    $"Cảnh báo chỉ số điện/nước bất thường (T{period.TargetMonth}/{period.TargetYear})",
                    $"Phát hiện {response.Flagged} chỉ số điện/nước bất thường khi nhập liệu tại block {request.BlockCode}. Vui lòng liên hệ bảo vệ kiểm tra phòng.",
                    "Utility",
                    period.Id.ToString());
            }

            // Nhánh Abnormal? = No -> Tự động tính phụ trội, chia đều chi phí và gửi thông báo công nợ cho sinh viên
            foreach (var rId in affectedRoomIds)
            {
                await ProcessRoomSurchargeAndNotifyAsync(period, rId);
            }

            return response;
        }

        public async Task<bool> UpdateReadingAsync(int id, decimal currentValue, string? note, int userId, bool isManager = false)
        {
            var reading = await _ewRepo.GetReadingByIdAsync(id)
                ?? throw new KeyNotFoundException($"Không tìm thấy chỉ số id = {id}");

            if (!isManager)
            {
                var guard = await _ewRepo.GetGuardByUserIdAsync(userId);
                if (guard == null)
                    throw new UnauthorizedAccessException("Tài khoản không phải bảo vệ hợp lệ hoặc chưa được gán Ký túc xá.");

                var readingDormId = reading.Room?.Block?.DormId;
                if (readingDormId != guard.DormId)
                {
                    var dormName = reading.Room?.Block?.Dorm?.Name ?? readingDormId?.ToString();
                    var guardDormName = guard.Dorm?.Name ?? guard.DormId.ToString();
                    throw new UnauthorizedAccessException($"Bảo vệ không có quyền chỉnh sửa chỉ số phòng {reading.Room?.RoomNumber} thuộc KTX {dormName}. Bạn chỉ được thao tác tại KTX {guardDormName}.");
                }
            }

            if (currentValue < 0)
                throw new ArgumentException("Chỉ số không được là số âm");

            reading.CurrentValue = currentValue;
            reading.Note = isManager ? (string.IsNullOrEmpty(note) ? "[Quản lý cập nhật]" : $"[Quản lý cập nhật] {note}") : note;

            if (!isManager)
            {
                reading.RecordedById = userId;
            }

            if (currentValue < reading.PreviousValue)
            {
                reading.Status = "Flagged";
                await _ewRepo.AddUsageAnomalyAsync(new UsageAnomaly
                {
                    ReadingId = reading.Id,
                    FlagReason = $"Chỉnh sửa: Chỉ số mới ({currentValue}) nhỏ hơn chỉ số cũ ({reading.PreviousValue})",
                    Status = "Flagged",
                    CreatedTime = DateTime.UtcNow
                });
            }
            else
            {
                reading.Status = isManager ? "Resolved" : "Recorded";

                if (isManager)
                {
                    var validManagerId = await _ewRepo.GetValidManagerIdAsync(userId);
                    var openAnomalies = await _ewRepo.GetActiveAnomaliesByReadingIdAsync(reading.Id);
                    if (openAnomalies != null)
                    {
                        foreach (var ua in openAnomalies)
                        {
                            ua.Status = "Resolved";
                            ua.Resolution = "Corrected";
                            ua.ManagerNote = note;
                            ua.ResolvedById = validManagerId;
                            ua.ResolvedTime = DateTime.UtcNow;
                        }
                    }
                }
            }

            await _ewRepo.SaveChangesAsync();

            if (reading.BillingPeriodId.HasValue && reading.Status != "Flagged")
            {
                var period = await _ewRepo.GetBillingPeriodByIdAsync(reading.BillingPeriodId.Value);
                await ProcessRoomSurchargeAndNotifyAsync(period, reading.RoomId);
            }

            return true;
        }

        public async Task<bool> DeleteReadingAsync(int id, int userId, bool isManager = false)
        {
            var reading = await _ewRepo.GetReadingByIdAsync(id)
                ?? throw new KeyNotFoundException($"Không tìm thấy chỉ số id = {id}");

            if (!isManager)
            {
                var guard = await _ewRepo.GetGuardByUserIdAsync(userId);
                if (guard == null)
                    throw new UnauthorizedAccessException("Tài khoản không phải bảo vệ hợp lệ hoặc chưa được gán Ký túc xá.");

                var readingDormId = reading.Room?.Block?.DormId;
                if (readingDormId != guard.DormId)
                {
                    var dormName = reading.Room?.Block?.Dorm?.Name ?? readingDormId?.ToString();
                    var guardDormName = guard.Dorm?.Name ?? guard.DormId.ToString();
                    throw new UnauthorizedAccessException($"Bảo vệ không có quyền xóa chỉ số phòng {reading.Room?.RoomNumber} thuộc KTX {dormName}. Bạn chỉ được thao tác tại KTX {guardDormName}.");
                }
            }

            _ewRepo.DeleteReading(reading);
            await _ewRepo.SaveChangesAsync();
            return true;
        }

        // ── I1, I2, I3, I4: Excel Import ────────────────────────
        public async Task<ExcelPreviewResponseDto> PreviewExcelImportAsync(Stream fileStream, int billingPeriodId, int? userId = null, bool isManager = false)
        {
            var period = await _ewRepo.GetBillingPeriodByIdAsync(billingPeriodId)
                ?? throw new KeyNotFoundException($"Không tìm thấy kỳ thanh toán id = {billingPeriodId}");

            var parsedRows = EwExcelParser.ParseExcelFile(fileStream);
            var existingReadings = await _ewRepo.GetReadingsByPeriodAsync(billingPeriodId);

            int? guardDormId = null;
            Guard? guard = null;
            if (!isManager && userId.HasValue)
            {
                guard = await _ewRepo.GetGuardByUserIdAsync(userId.Value);
                if (guard == null)
                    throw new UnauthorizedAccessException("Tài khoản không phải bảo vệ hợp lệ hoặc chưa được gán Ký túc xá.");
                guardDormId = guard.DormId;
            }

            int validCount = 0;
            int warningCount = 0;
            int errorCount = 0;

            foreach (var row in parsedRows)
            {
                if (row.Status == "Error")
                {
                    errorCount++;
                    continue;
                }

                var rooms = await _ewRepo.GetRoomsByBlockCodeAsync(row.BlockCode);
                if (!string.IsNullOrWhiteSpace(row.Dorm) && rooms.Count > 1)
                {
                    var normRowDorm = row.Dorm.Trim().ToUpper();
                    if (normRowDorm.StartsWith("DOM-")) normRowDorm = normRowDorm.Substring(4).Trim();
                    if (normRowDorm.StartsWith("DOM ")) normRowDorm = normRowDorm.Substring(4).Trim();
                    if (normRowDorm.StartsWith("DORM-")) normRowDorm = normRowDorm.Substring(5).Trim();
                    if (normRowDorm.StartsWith("DORM ")) normRowDorm = normRowDorm.Substring(5).Trim();

                    var roomsByDorm = rooms.Where(r =>
                    {
                        var dCode = (r.Block?.Dorm?.Code ?? string.Empty).Trim().ToUpper();
                        if (dCode.StartsWith("DOM-")) dCode = dCode.Substring(4).Trim();
                        var dName = (r.Block?.Dorm?.Name ?? string.Empty).Trim().ToUpper();
                        return dCode == normRowDorm || dName == row.Dorm.Trim().ToUpper() || dName.EndsWith(" " + normRowDorm);
                    }).ToList();

                    if (roomsByDorm.Any())
                    {
                        rooms = roomsByDorm;
                    }
                }

                if (!rooms.Any())
                {
                    row.Status = "Error";
                    row.ErrorMessage = $"Không tìm thấy phòng tương ứng cho block '{row.BlockCode}'";
                    errorCount++;
                    continue;
                }

                if (guardDormId.HasValue)
                {
                    var roomDifferentDorm = rooms.FirstOrDefault(r => r.Block.DormId != guardDormId.Value);
                    if (roomDifferentDorm != null)
                    {
                        var blockDormName = roomDifferentDorm.Block.Dorm?.Name ?? roomDifferentDorm.Block.DormId.ToString();
                        var guardDormName = guard?.Dorm?.Name ?? guardDormId.Value.ToString();
                        row.Status = "Error";
                        row.ErrorMessage = $"Toà/phòng '{row.BlockCode}' thuộc KTX {blockDormName}. Bạn chỉ được import dữ liệu cho KTX {guardDormName}.";
                        errorCount++;
                        continue;
                    }
                }

                var roomL = rooms.FirstOrDefault(r => r.RoomNumber.EndsWith("L", StringComparison.OrdinalIgnoreCase)) ?? rooms.First();
                var roomR = rooms.FirstOrDefault(r => r.RoomNumber.EndsWith("R", StringComparison.OrdinalIgnoreCase)) ?? (rooms.Count > 1 ? rooms.Last() : null);

                row.LeftRoomId = roomL?.Id;
                row.LeftRoomNumber = roomL?.RoomNumber ?? string.Empty;
                row.RightRoomId = roomR?.Id;
                row.RightRoomNumber = roomR?.RoomNumber ?? string.Empty;

                if (roomL != null)
                {
                    var oldL = await _ewRepo.GetLatestReadingBeforePeriodAsync(roomL.Id, row.MeterType, period.TargetMonth, period.TargetYear);
                    row.LeftOldReading = oldL?.CurrentValue ?? 0;
                    row.LeftConsumption = row.LeftValue.HasValue ? (row.LeftValue.Value - row.LeftOldReading) : 0;
                }

                if (roomR != null)
                {
                    var oldR = await _ewRepo.GetLatestReadingBeforePeriodAsync(roomR.Id, row.MeterType, period.TargetMonth, period.TargetYear);
                    row.RightOldReading = oldR?.CurrentValue ?? 0;
                    row.RightConsumption = row.RightValue.HasValue ? (row.RightValue.Value - row.RightOldReading) : 0;
                }

                // Kiểm tra cảnh báo bất thường (bao gồm cả tiêu thụ vượt quá 1000 số điện / 50 số nước sau khi trừ định mức)
                bool hasWarning = false;
                var warningMsgs = new List<string>();

                if (row.LeftValue.HasValue && roomL != null)
                {
                    var prevTermConsL = await _ewRepo.GetRoomTermConsumptionBeforePeriodAsync(roomL.Id, row.MeterType, period.TermId, period.Id);
                    var (isFlaggedL, reasonL) = CheckReadingAnomaly(row.MeterType, row.LeftOldReading, row.LeftValue.Value, prevTermConsL);
                    if (isFlaggedL && !string.IsNullOrEmpty(reasonL))
                    {
                        hasWarning = true;
                        warningMsgs.Add($"Phòng {row.LeftRoomNumber}: {reasonL}");
                    }
                }

                if (row.RightValue.HasValue && row.RightRoomId.HasValue && roomR != null)
                {
                    var prevTermConsR = await _ewRepo.GetRoomTermConsumptionBeforePeriodAsync(roomR.Id, row.MeterType, period.TermId, period.Id);
                    var (isFlaggedR, reasonR) = CheckReadingAnomaly(row.MeterType, row.RightOldReading, row.RightValue.Value, prevTermConsR);
                    if (isFlaggedR && !string.IsNullOrEmpty(reasonR))
                    {
                        hasWarning = true;
                        warningMsgs.Add($"Phòng {row.RightRoomNumber}: {reasonR}");
                    }
                }

                if (hasWarning)
                {
                    row.Status = "Warning";
                    row.FlagReason = string.Join("; ", warningMsgs);
                    warningCount++;
                }
                else
                {
                    row.Status = "Valid";
                    validCount++;
                }
            }

            return new ExcelPreviewResponseDto
            {
                TotalRows = parsedRows.Count,
                ValidRows = validCount,
                WarningRows = warningCount,
                ErrorRows = errorCount,
                AlreadyHasDataInPeriod = existingReadings != null && existingReadings.Any(),
                Rows = parsedRows
            };
        }

        public async Task<ExcelConfirmImportResponseDto> ConfirmExcelImportAsync(int guardUserId, ExcelConfirmImportRequestDto request, bool isManager = false)
        {
            var period = await _ewRepo.GetBillingPeriodByIdAsync(request.BillingPeriodId)
                ?? throw new KeyNotFoundException($"Không tìm thấy kỳ thanh toán id = {request.BillingPeriodId}");

            if (period.Status != "Open")
                throw new InvalidOperationException($"Kỳ thanh toán Tháng {period.TargetMonth}/{period.TargetYear} hiện không mở ({period.Status})");

            Guard? guard = null;
            if (!isManager)
            {
                guard = await _ewRepo.GetGuardByUserIdAsync(guardUserId);
                if (guard == null)
                    throw new UnauthorizedAccessException("Tài khoản không phải bảo vệ hợp lệ hoặc chưa được gán Ký túc xá.");
            }

            var blocks = await _ewRepo.GetAllBlocksAsync() ?? new List<Block>();
            if (guard != null)
            {
                blocks = blocks.Where(b => b.DormId == guard.DormId).ToList();
            }

            var firstBlockCode = request.Rows.FirstOrDefault(r => !string.IsNullOrEmpty(r.BlockCode))?.BlockCode;
            var matchedBlock = blocks.FirstOrDefault(b => string.Equals(b.Code, firstBlockCode, StringComparison.OrdinalIgnoreCase))
                            ?? blocks.FirstOrDefault();
            int targetBlockId = matchedBlock?.Id ?? 1;

            var batch = new ImportBatch
            {
                BillingPeriodId = period.Id,
                BlockId = targetBlockId,
                GuardId = guardUserId,
                Method = "Excel",
                FileName = request.FileName,
                TotalRows = request.Rows.Count,
                ErrorRows = 0,
                ImportTime = DateTime.UtcNow
            };

            await _ewRepo.AddImportBatchAsync(batch);

            int savedReadings = 0;
            int flaggedReadings = 0;
            int errorRows = 0;
            var affectedRoomIds = new HashSet<int>();

            foreach (var row in request.Rows)
            {
                if (row.Status == "Error")
                {
                    errorRows++;
                    if (!request.ImportOnlyValid)
                    {
                        throw new ArgumentException($"Dòng {row.RowIndex} có lỗi: {row.ErrorMessage}");
                    }
                    continue;
                }

                if (guard != null)
                {
                    var rowRooms = await _ewRepo.GetRoomsByBlockCodeAsync(row.BlockCode);
                    if (!string.IsNullOrWhiteSpace(row.Dorm) && rowRooms.Count > 1)
                    {
                        var normRowDorm = row.Dorm.Trim().ToUpper();
                        if (normRowDorm.StartsWith("DOM-")) normRowDorm = normRowDorm.Substring(4).Trim();
                        if (normRowDorm.StartsWith("DOM ")) normRowDorm = normRowDorm.Substring(4).Trim();
                        if (normRowDorm.StartsWith("DORM-")) normRowDorm = normRowDorm.Substring(5).Trim();
                        if (normRowDorm.StartsWith("DORM ")) normRowDorm = normRowDorm.Substring(5).Trim();

                        var roomsByDorm = rowRooms.Where(r =>
                        {
                            var dCode = (r.Block?.Dorm?.Code ?? string.Empty).Trim().ToUpper();
                            if (dCode.StartsWith("DOM-")) dCode = dCode.Substring(4).Trim();
                            var dName = (r.Block?.Dorm?.Name ?? string.Empty).Trim().ToUpper();
                            return dCode == normRowDorm || dName == row.Dorm.Trim().ToUpper() || dName.EndsWith(" " + normRowDorm);
                        }).ToList();

                        if (roomsByDorm.Any())
                        {
                            rowRooms = roomsByDorm;
                        }
                    }

                    if (rowRooms.Any(r => r.Block.DormId != guard.DormId))
                    {
                        errorRows++;
                        if (!request.ImportOnlyValid)
                        {
                            throw new UnauthorizedAccessException($"Dòng {row.RowIndex}: Toà/phòng '{row.BlockCode}' không thuộc KTX {guard.Dorm?.Name ?? guard.DormId.ToString()} bạn quản lý.");
                        }
                        continue;
                    }
                }

                // Cặp phòng L và R
                var pairs = new List<(int? RoomId, string RoomNumber, decimal? NewVal, decimal OldVal)>
                {
                    (row.LeftRoomId, row.LeftRoomNumber, row.LeftValue, row.LeftOldReading),
                    (row.RightRoomId, row.RightRoomNumber, row.RightValue, row.RightOldReading)
                };

                foreach (var p in pairs)
                {
                    if (!p.RoomId.HasValue || !p.NewVal.HasValue) continue;

                    var prevTermCons = await _ewRepo.GetRoomTermConsumptionBeforePeriodAsync(p.RoomId.Value, row.MeterType, period.TermId, period.Id);
                    var existing = await _ewRepo.GetReadingForPeriodAsync(period.Id, p.RoomId.Value, row.MeterType);
                    var reading = existing ?? new MeterReading
                    {
                        BillingPeriodId = period.Id,
                        RoomId = p.RoomId.Value,
                        MeterType = row.MeterType,
                        Source = "Import",
                        BatchId = batch.Id
                    };

                    reading.PreviousValue = p.OldVal;
                    reading.CurrentValue = p.NewVal.Value;
                    reading.RecordedById = guardUserId;
                    reading.RecordedTime = DateTime.UtcNow;

                    var (isFlagged, anomalyReason) = CheckReadingAnomaly(row.MeterType, p.OldVal, p.NewVal.Value, prevTermCons);

                    reading.Status = isFlagged ? "Flagged" : "Recorded";

                    if (existing == null)
                    {
                        await _ewRepo.AddMeterReadingAsync(reading);
                    }

                    await _ewRepo.SaveChangesAsync();
                    affectedRoomIds.Add(p.RoomId.Value);

                    if (isFlagged && !string.IsNullOrEmpty(anomalyReason))
                    {
                        var anomaly = new UsageAnomaly
                        {
                            ReadingId = reading.Id,
                            FlagReason = anomalyReason,
                            Status = "Flagged",
                            CreatedTime = DateTime.UtcNow
                        };
                        await _ewRepo.AddUsageAnomalyAsync(anomaly);
                        await _ewRepo.SaveChangesAsync();
                        flaggedReadings++;
                    }

                    savedReadings++;
                }
            }

            batch.ErrorRows = errorRows;
            await _ewRepo.SaveChangesAsync();

            if (_inAppNotificationService != null && flaggedReadings > 0)
            {
                await _inAppNotificationService.NotifyRolesAsync(
                    new[] { AccountConstants.Roles.Manager },
                    $"Cảnh báo chỉ số điện/nước bất thường (T{period.TargetMonth}/{period.TargetYear})",
                    $"File import '{request.FileName}' phát hiện {flaggedReadings} chỉ số bất thường. Vui lòng liên hệ bảo vệ kiểm tra phòng và xác nhận/điều chỉnh số liệu.",
                    "Utility",
                    period.Id.ToString());
            }

            // Nhánh Abnormal? = No -> Tự động tính phụ trội, chia đều chi phí và gửi thông báo công nợ cho các phòng không bị Flagged
            foreach (var rId in affectedRoomIds)
            {
                await ProcessRoomSurchargeAndNotifyAsync(period, rId);
            }

            return new ExcelConfirmImportResponseDto
            {
                BatchId = batch.Id,
                ImportTime = batch.ImportTime,
                TotalRows = request.Rows.Count,
                SavedReadings = savedReadings,
                FlaggedReadings = flaggedReadings,
                ErrorRows = errorRows,
                ManagerAlerted = flaggedReadings > 0,
                Message = $"Import thành công {savedReadings} chỉ số ({flaggedReadings} chỉ số bất thường đã gửi cảnh báo)."
            };
        }

        public async Task<List<ImportBatchItemDto>> GetImportBatchesAsync(int? billingPeriodId, int? userId = null, bool isManager = false)
        {
            int? dormId = null;
            if (!isManager && userId.HasValue)
            {
                dormId = await _ewRepo.GetGuardDormIdAsync(userId.Value);
            }

            var batches = await _ewRepo.GetImportBatchesAsync(billingPeriodId, dormId);
            return batches.Select(b => new ImportBatchItemDto
            {
                Id = b.Id,
                BillingPeriodId = b.BillingPeriodId,
                BillingPeriodDisplay = $"Tháng {b.BillingPeriod.TargetMonth}/{b.BillingPeriod.TargetYear}",
                BlockCode = b.Block?.Code ?? string.Empty,
                GuardName = b.Guard?.User?.FullName ?? string.Empty,
                Method = b.Method,
                FileName = b.FileName,
                TotalRows = b.TotalRows,
                ErrorRows = b.ErrorRows,
                ImportTime = b.ImportTime
            }).ToList();
        }

        public byte[] GenerateExcelTemplate() => EwExcelParser.GenerateTemplate();

        public async Task<byte[]> GenerateExcelTemplateAsync(int? billingPeriodId, string? dormCode, string? meterType, int? userId = null, bool isManager = false)
        {
            int? guardDormId = null;
            if (!isManager && userId.HasValue)
            {
                var guard = await _ewRepo.GetGuardByUserIdAsync(userId.Value);
                if (guard != null)
                {
                    guardDormId = guard.DormId;
                    if (guard.Dorm != null)
                    {
                        dormCode = guard.Dorm.Code;
                    }
                }
            }

            BillingPeriod? period = null;
            if (billingPeriodId.HasValue)
            {
                period = await _ewRepo.GetBillingPeriodByIdAsync(billingPeriodId.Value);
            }

            if (period == null)
            {
                var periods = await _ewRepo.GetAllBillingPeriodsAsync();
                period = periods.FirstOrDefault(p => p.Status == "Open") ?? periods.FirstOrDefault();
            }

            string dateStr;
            if (period != null)
            {
                int daysInMonth = DateTime.DaysInMonth(period.TargetYear, period.TargetMonth);
                dateStr = new DateTime(period.TargetYear, period.TargetMonth, daysInMonth).ToString("dd/MM/yyyy");
            }
            else
            {
                dateStr = DateTime.Today.ToString("dd/MM/yyyy");
            }

            int termId = period?.TermId ?? 1;

            var typesToGenerate = new List<string>();
            if (string.IsNullOrWhiteSpace(meterType) ||
                meterType.Equals("ALL", StringComparison.OrdinalIgnoreCase) ||
                meterType.Equals("BOTH", StringComparison.OrdinalIgnoreCase) ||
                meterType.Equals("EW", StringComparison.OrdinalIgnoreCase))
            {
                typesToGenerate.Add("E");
                typesToGenerate.Add("W");
            }
            else if (meterType.StartsWith("E", StringComparison.OrdinalIgnoreCase) || meterType.Equals("Electricity", StringComparison.OrdinalIgnoreCase))
            {
                typesToGenerate.Add("E");
            }
            else
            {
                typesToGenerate.Add("W");
            }

            string dorm = "A";
            if (!string.IsNullOrWhiteSpace(dormCode))
            {
                dorm = dormCode.Trim().ToUpper();
                if (dorm.StartsWith("DOM-")) dorm = dorm.Substring(4);
                if (dorm.StartsWith("DORM-")) dorm = dorm.Substring(5);
            }

            var allBlocks = await _ewRepo.GetAllBlocksAsync() ?? new List<Block>();
            List<Block> filteredBlocks;

            if (!isManager && guardDormId.HasValue)
            {
                // Bảo vệ: CHỈ lấy các phòng/block thuộc tòa KTX mà bảo vệ đó được phân công quản lý
                filteredBlocks = allBlocks
                    .Where(b => b.DormId == guardDormId.Value)
                    .OrderBy(b => b.Code)
                    .ToList();
            }
            else
            {
                // Quản lý (Manager): Lấy toàn bộ các phòng/block của tất cả các Dorm (hoặc lọc nếu có truyền dormCode)
                filteredBlocks = allBlocks
                    .Where(b =>
                    {
                        if (string.IsNullOrWhiteSpace(dormCode)) return true;
                        var dCode = b.Dorm?.Code ?? string.Empty;
                        var normDorm = dormCode.Trim().ToUpper();
                        return string.Equals(dCode, normDorm, StringComparison.OrdinalIgnoreCase)
                            || string.Equals(dCode, "DOM-" + normDorm, StringComparison.OrdinalIgnoreCase)
                            || (normDorm.StartsWith("DOM-") && string.Equals(dCode, normDorm, StringComparison.OrdinalIgnoreCase));
                    })
                    .OrderBy(b => b.Dorm?.Code ?? string.Empty)
                    .ThenBy(b => b.Code)
                    .ToList();

                if (!filteredBlocks.Any())
                {
                    filteredBlocks = allBlocks
                        .OrderBy(b => b.Dorm?.Code ?? string.Empty)
                        .ThenBy(b => b.Code)
                        .ToList();
                }
            }

            var rows = new List<ExcelTemplateRowModel>();
            foreach (var b in filteredBlocks)
            {
                var dCode = b.Dorm?.Code ?? dorm;
                if (dCode.StartsWith("DOM-", StringComparison.OrdinalIgnoreCase))
                {
                    dCode = dCode.Substring(4);
                }

                foreach (var t in typesToGenerate)
                {
                    rows.Add(new ExcelTemplateRowModel
                    {
                        Dorm = dCode,
                        Block = b.Code,
                        Type = t,
                        Date = dateStr,
                        L = null,
                        R = null,
                        Term = termId
                    });
                }
            }

            return EwExcelParser.GenerateTemplateFromData(rows);
        }

        // ── A1, A2: Anomalies & Manager Adjust/Confirm ──────────
        public async Task<List<UsageAnomalyItemDto>> GetAnomaliesAsync(int? billingPeriodId, string? status)
        {
            var anomalies = await _ewRepo.GetAnomaliesAsync(billingPeriodId, status);
            return anomalies.Select(ua =>
            {
                var reading = ua.Reading;
                var room = reading?.Room;
                var block = room?.Block;
                var dorm = block?.Dorm;
                var period = reading?.BillingPeriod;
                var isElec = (reading?.MeterType ?? "").Contains("Elec", StringComparison.OrdinalIgnoreCase) ||
                             (reading?.MeterType ?? "").Contains("Điện", StringComparison.OrdinalIgnoreCase);
                decimal quota = isElec ? ElectricTermQuota : WaterTermQuota;
                decimal prevVal = reading?.PreviousValue ?? 0m;
                decimal curVal = reading?.CurrentValue ?? 0m;
                decimal cons = reading?.Consumption ?? (curVal - prevVal);

                return new UsageAnomalyItemDto
                {
                    AnomalyId = ua.Id,
                    ReadingId = ua.ReadingId,
                    BillingPeriodId = reading?.BillingPeriodId,
                    BillingPeriodDisplay = period != null ? $"Tháng {period.TargetMonth}/{period.TargetYear}" : string.Empty,
                    RoomId = reading?.RoomId ?? 0,
                    RoomNumber = room?.RoomNumber ?? string.Empty,
                    BlockCode = block?.Code ?? string.Empty,
                    BlockName = block?.Name ?? string.Empty,
                    DormName = dorm?.Name ?? "Dom A",
                    MeterType = reading?.MeterType ?? string.Empty,
                    PreviousValue = prevVal,
                    CurrentValue = curVal,
                    Consumption = cons,
                    Quota = quota,
                    OverUsage = cons - quota,
                    FlagReason = ua.FlagReason,
                    Status = ua.Status,
                    Resolution = ua.Resolution,
                    ManagerNote = ua.ManagerNote,
                    GuardName = reading?.RecordedBy?.User?.FullName ?? "Bảo vệ",
                    GuardPhone = reading?.RecordedBy?.User?.Phone,
                    CreatedTime = ua.CreatedTime,
                    ResolvedTime = ua.ResolvedTime,
                    ResolvedByName = ua.ResolvedBy?.User?.FullName
                };
            }).ToList();
        }

        public async Task<ResolveAnomalyResponseDto> ResolveAnomalyAsync(int anomalyId, ResolveAnomalyRequestDto request, int managerUserId)
        {
            var anomaly = await _ewRepo.GetAnomalyByIdAsync(anomalyId)
                ?? throw new KeyNotFoundException($"Không tìm thấy cảnh báo bất thường id = {anomalyId}");

            var reading = anomaly.Reading
                ?? throw new KeyNotFoundException($"Không tìm thấy chỉ số gắn với cảnh báo id = {anomalyId}");

            var action = (request.Action ?? "Confirm").Trim();
            bool isAdjust = action.Equals("Adjust", StringComparison.OrdinalIgnoreCase) ||
                            action.Equals("Corrected", StringComparison.OrdinalIgnoreCase);

            var validManagerId = await _ewRepo.GetValidManagerIdAsync(managerUserId);

            if (isAdjust)
            {
                if (!request.AdjustedValue.HasValue)
                    throw new ArgumentException("Vui lòng nhập chỉ số điều chỉnh mới (AdjustedValue).");

                if (request.AdjustedValue.Value < 0)
                    throw new ArgumentException("Chỉ số điều chỉnh không được là số âm.");

                if (request.AdjustedValue.Value < reading.PreviousValue)
                    throw new ArgumentException($"Chỉ số điều chỉnh mới ({request.AdjustedValue.Value}) không được nhỏ hơn chỉ số kỳ trước ({reading.PreviousValue}).");

                reading.CurrentValue = request.AdjustedValue.Value;
                reading.Status = "Resolved";
                reading.Note = string.IsNullOrWhiteSpace(request.ManagerNote)
                    ? "[Quản lý điều chỉnh sau khi bảo vệ kiểm tra phòng]"
                    : $"[Quản lý điều chỉnh] {request.ManagerNote}";

                anomaly.Resolution = "Corrected";
            }
            else
            {
                if (reading.CurrentValue < reading.PreviousValue)
                    throw new ArgumentException($"Chỉ số hiện tại ({reading.CurrentValue}) đang nhỏ hơn chỉ số cũ ({reading.PreviousValue}). Vui lòng chọn 'Điều chỉnh (Adjust)' để sửa lại chỉ số đúng.");

                reading.Status = "Resolved";
                reading.Note = string.IsNullOrWhiteSpace(request.ManagerNote)
                    ? "[Quản lý xác nhận giữ nguyên số liệu sau khi bảo vệ kiểm tra phòng]"
                    : $"[Quản lý xác nhận] {request.ManagerNote}";

                anomaly.Resolution = "Accepted";
            }

            anomaly.Status = "Resolved";
            anomaly.ManagerNote = request.ManagerNote;
            anomaly.ResolvedById = validManagerId;
            anomaly.ResolvedTime = DateTime.UtcNow;

            var otherOpenAnomalies = await _ewRepo.GetActiveAnomaliesByReadingIdAsync(reading.Id);
            if (otherOpenAnomalies != null)
            {
                foreach (var ua in otherOpenAnomalies)
                {
                    if (ua.Id == anomaly.Id) continue;
                    ua.Status = "Resolved";
                    ua.Resolution = anomaly.Resolution;
                    ua.ManagerNote = request.ManagerNote;
                    ua.ResolvedById = validManagerId;
                    ua.ResolvedTime = DateTime.UtcNow;
                }
            }

            await _ewRepo.SaveChangesAsync();

            RoomSurchargeCalculationResult? calcResult = null;
            if (reading.BillingPeriodId.HasValue)
            {
                var period = reading.BillingPeriod ?? await _ewRepo.GetBillingPeriodByIdAsync(reading.BillingPeriodId.Value);
                calcResult = await ProcessRoomSurchargeAndNotifyAsync(period, reading.RoomId);
            }

            decimal finalConsumption = reading.CurrentValue - reading.PreviousValue;
            string actionText = isAdjust ? "điều chỉnh" : "xác nhận";
            string billText = calcResult?.Calculated == true
                ? (calcResult.SharePerStudent > 0
                    ? $" Đã tính phụ trội ({calcResult.SharePerStudent:N0} VNĐ/SV) và gửi thông báo công nợ cho {calcResult.NotifiedStudentIds.Count} sinh viên."
                    : " Phòng nằm trong định mức, không phát sinh công nợ phụ trội.")
                : " (Phòng vẫn còn chỉ số khác đang chờ duyệt hoặc chưa đủ dữ liệu kỳ).";

            return new ResolveAnomalyResponseDto
            {
                AnomalyId = anomaly.Id,
                ReadingId = reading.Id,
                Status = anomaly.Status,
                Resolution = anomaly.Resolution ?? "Accepted",
                FinalReadingValue = reading.CurrentValue,
                FinalConsumption = finalConsumption,
                SurchargeCalculated = calcResult?.Calculated ?? false,
                DebtNotificationsSent = calcResult?.NotifiedStudentIds.Count ?? 0,
                SharePerStudent = calcResult?.SharePerStudent,
                Message = $"Đã {actionText} số liệu phòng {reading.Room?.RoomNumber} thành công.{billText}"
            };
        }

        // ── B1: Surcharge Calculation & Debt Notification ───────
        public async Task<BillingCalculationResultDto> CalculatePeriodSurchargesAsync(int billingPeriodId)
        {
            var period = await _ewRepo.GetBillingPeriodByIdAsync(billingPeriodId)
                ?? throw new KeyNotFoundException($"Không tìm thấy kỳ thanh toán id = {billingPeriodId}");

            var readings = await _ewRepo.GetReadingsByPeriodAsync(billingPeriodId) ?? new List<MeterReading>();
            var roomIds = readings.Select(r => r.RoomId).Distinct().ToList();

            int roomsCalculated = 0;
            int roomsSkippedFlagged = 0;
            int invoicesCreated = 0;
            int notificationsSent = 0;

            foreach (var roomId in roomIds)
            {
                var res = await ProcessRoomSurchargeAndNotifyAsync(period, roomId);
                if (res.HasFlaggedReadings)
                {
                    roomsSkippedFlagged++;
                }
                else if (res.Calculated)
                {
                    roomsCalculated++;
                    invoicesCreated += res.InvoicesCreated;
                    notificationsSent += res.NotifiedStudentIds.Count;
                }
            }

            return new BillingCalculationResultDto
            {
                BillingPeriodId = billingPeriodId,
                RoomsCalculated = roomsCalculated,
                RoomsSkippedFlagged = roomsSkippedFlagged,
                InvoicesCreated = invoicesCreated,
                NotificationsSent = notificationsSent,
                Message = $"Đã tính phí phụ trội cho {roomsCalculated} phòng, tạo {invoicesCreated} hóa đơn và gửi {notificationsSent} thông báo công nợ" +
                          (roomsSkippedFlagged > 0 ? $" (bỏ qua {roomsSkippedFlagged} phòng đang có cảnh báo Flagged chờ Manager xử lý)." : ".")
            };
        }

        private async Task<RoomSurchargeCalculationResult> ProcessRoomSurchargeAndNotifyAsync(BillingPeriod? period, int roomId)
        {
            if (period == null)
                return new RoomSurchargeCalculationResult { RoomId = roomId, Calculated = false };

            var calcResult = await _ewRepo.CalculateAndPublishRoomSurchargeAsync(period.Id, roomId);
            if (calcResult == null)
                return new RoomSurchargeCalculationResult { RoomId = roomId, Calculated = false };

            if (calcResult.Calculated && calcResult.NotifiedStudentIds.Any() && calcResult.SharePerStudent > 0 && _inAppNotificationService != null)
            {
                await _inAppNotificationService.NotifyUsersAsync(
                    calcResult.NotifiedStudentIds,
                    $"Thông báo công nợ điện nước (T{period.TargetMonth}/{period.TargetYear}) - Phòng {calcResult.RoomNumber}",
                    $"Phòng {calcResult.RoomNumber} phát sinh phí phụ trội điện/nước vượt định mức kỳ T{period.TargetMonth}/{period.TargetYear}. Tổng phụ trội: {calcResult.TotalCost:N0} VNĐ, chia đều {calcResult.NumberOfResidents} sinh viên: {calcResult.SharePerStudent:N0} VNĐ/người. Vui lòng kiểm tra và thanh toán đúng hạn.",
                    "Utility",
                    period.Id.ToString());
            }

            return calcResult;
        }

        // ── Helpers & Anomaly Rules ─────────────────────────────
        public const decimal ElectricTermQuota = 200m;
        public const decimal WaterTermQuota = 12m;
        public const decimal ElectricAnomalyOverQuotaThreshold = 1000m;
        public const decimal WaterAnomalyOverQuotaThreshold = 50m;

        public static (bool IsFlagged, string? AnomalyReason) CheckReadingAnomaly(
            string meterType,
            decimal oldReading,
            decimal newReading,
            decimal previousTermConsumption = 0m)
        {
            if (newReading < oldReading)
            {
                return (true, $"Chỉ số mới ({newReading}) nhỏ hơn chỉ số cũ ({oldReading})");
            }

            decimal consumption = newReading - oldReading;
            if (consumption == 0)
            {
                return (true, "Mức tiêu thụ bằng 0, nghi vấn bỏ sót hoặc đồng hồ kẹt");
            }

            bool isElec = meterType.Contains("Elec", StringComparison.OrdinalIgnoreCase) ||
                          meterType.Contains("Điện", StringComparison.OrdinalIgnoreCase) ||
                          meterType.Equals("E", StringComparison.OrdinalIgnoreCase);

            decimal quota = isElec ? ElectricTermQuota : WaterTermQuota;
            decimal threshold = isElec ? ElectricAnomalyOverQuotaThreshold : WaterAnomalyOverQuotaThreshold;
            decimal totalTermConsumption = previousTermConsumption + consumption;
            decimal overUsage = totalTermConsumption - quota;

            if (overUsage > threshold)
            {
                if (isElec)
                {
                    return (true, $"Tiêu thụ điện vượt định mức {overUsage:N0} kWh (> {threshold:N0} kWh sau khi trừ định mức {quota:N0} kWh), nghi vấn sử dụng thiết bị điện công suất lớn/đồ cấm (bếp từ...)");
                }
                else
                {
                    return (true, $"Tiêu thụ nước vượt định mức {overUsage:N0} m³ (> {threshold:N0} m³ sau khi trừ định mức {quota:N0} m³), nghi vấn rò rỉ đường ống nước");
                }
            }

            return (false, null);
        }

        private static string GetBaseRoomCode(string roomNumber)
        {
            if (string.IsNullOrWhiteSpace(roomNumber)) return string.Empty;
            if (roomNumber.Length > 1 && (roomNumber.EndsWith("L", StringComparison.OrdinalIgnoreCase) || roomNumber.EndsWith("R", StringComparison.OrdinalIgnoreCase)))
            {
                return roomNumber.Substring(0, roomNumber.Length - 1);
            }
            return roomNumber;
        }

        private static ReadingPreviewItemDto EvaluateReading(
            int roomId,
            string roomNumber,
            string meterType,
            decimal oldReading,
            decimal newReading,
            decimal previousTermConsumption = 0m)
        {
            if (newReading < 0)
            {
                return new ReadingPreviewItemDto
                {
                    RoomId = roomId,
                    RoomNumber = roomNumber,
                    MeterType = meterType,
                    OldReading = oldReading,
                    NewReading = newReading,
                    Consumption = 0,
                    Status = "Error",
                    ErrorMessage = "Chỉ số mới không được là số âm"
                };
            }

            decimal consumption = newReading - oldReading;
            var (isFlagged, anomalyReason) = CheckReadingAnomaly(meterType, oldReading, newReading, previousTermConsumption);

            if (isFlagged)
            {
                return new ReadingPreviewItemDto
                {
                    RoomId = roomId,
                    RoomNumber = roomNumber,
                    MeterType = meterType,
                    OldReading = oldReading,
                    NewReading = newReading,
                    Consumption = consumption,
                    Status = "Warning",
                    Flagged = true,
                    FlagReason = anomalyReason
                };
            }

            return new ReadingPreviewItemDto
            {
                RoomId = roomId,
                RoomNumber = roomNumber,
                MeterType = meterType,
                OldReading = oldReading,
                NewReading = newReading,
                Consumption = consumption,
                Status = "Valid",
                Flagged = false
            };
        }

        // ── Student EW Usage ────────────────────────────────────
        public async Task<List<StudentTermOptionDto>> GetStudentTermsAsync(int studentUserId)
        {
            return await _ewRepo.GetStudentTermsAsync(studentUserId);
        }

        public async Task<StudentEwReportDto?> GetStudentEwReportAsync(int studentUserId, int? termId)
        {
            return await _ewRepo.GetStudentEwReportAsync(studentUserId, termId);
        }
    }
}
