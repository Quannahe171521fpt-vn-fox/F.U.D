using FUD.DTOs.Utilities;
using FUD.Infrastructure.Excel;
using FUD.Models;
using FUD.Repositories.Interfaces.Utilities;
using FUD.Services.Interfaces.Utilities;

namespace FUD.Services.Utilities
{
    public class EwService : IEwService
    {
        private readonly IEwRepository _ewRepo;

        public EwService(IEwRepository ewRepo)
        {
            _ewRepo = ewRepo;
        }

        public async Task<IEnumerable<object>> GetUsagesAsync(string? blockCode, string? meterType, int? month, int? year, int? termId = null, int? billingPeriodId = null)
        {
            return await _ewRepo.GetGroupedUsagesAsync(blockCode, meterType, month, year, termId, billingPeriodId);
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
        public async Task<BlockProgressResponseDto> GetBlockProgressAsync(int billingPeriodId)
        {
            var period = await _ewRepo.GetBillingPeriodByIdAsync(billingPeriodId)
                ?? throw new KeyNotFoundException($"Không tìm thấy kỳ thanh toán id = {billingPeriodId}");

            var allRooms = await _ewRepo.GetAllActiveRoomsWithBlockAsync();
            var readings = await _ewRepo.GetReadingsByPeriodAsync(billingPeriodId);

            var blockGroups = allRooms
                .GroupBy(r => new {
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
        public async Task<BlockFormDataResponseDto> GetBlockFormDataAsync(string blockCode, int billingPeriodId)
        {
            var period = await _ewRepo.GetBillingPeriodByIdAsync(billingPeriodId)
                ?? throw new KeyNotFoundException($"Không tìm thấy kỳ thanh toán id = {billingPeriodId}");

            var rooms = await _ewRepo.GetRoomsByBlockCodeAsync(blockCode);
            if (!rooms.Any())
                throw new KeyNotFoundException($"Không tìm thấy block/phòng có mã '{blockCode}'");

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
        public async Task<ManualPreviewResponseDto> PreviewManualReadingAsync(ManualPreviewRequestDto request)
        {
            var period = await _ewRepo.GetBillingPeriodByIdAsync(request.BillingPeriodId)
                ?? throw new KeyNotFoundException($"Không tìm thấy kỳ thanh toán id = {request.BillingPeriodId}");

            var rooms = await _ewRepo.GetRoomsByBlockCodeAsync(request.BlockCode);
            if (!rooms.Any())
                throw new KeyNotFoundException($"Không tìm thấy phòng tương ứng mã block '{request.BlockCode}'");

            var roomL = rooms.FirstOrDefault(r => r.RoomNumber.EndsWith("L", StringComparison.OrdinalIgnoreCase)) ?? rooms.First();
            var roomR = rooms.FirstOrDefault(r => r.RoomNumber.EndsWith("R", StringComparison.OrdinalIgnoreCase)) ?? (rooms.Count > 1 ? rooms.Last() : null);

            var items = new List<ReadingPreviewItemDto>();

            if (request.ElectricityLeftNewValue.HasValue && roomL != null)
            {
                var oldReading = (await _ewRepo.GetLatestReadingBeforePeriodAsync(roomL.Id, "Electricity", period.TargetMonth, period.TargetYear))?.CurrentValue ?? 0;
                items.Add(EvaluateReading(roomL.Id, roomL.RoomNumber, "Electricity", oldReading, request.ElectricityLeftNewValue.Value));
            }

            if (request.ElectricityRightNewValue.HasValue && roomR != null && roomR.Id != roomL?.Id)
            {
                var oldReading = (await _ewRepo.GetLatestReadingBeforePeriodAsync(roomR.Id, "Electricity", period.TargetMonth, period.TargetYear))?.CurrentValue ?? 0;
                items.Add(EvaluateReading(roomR.Id, roomR.RoomNumber, "Electricity", oldReading, request.ElectricityRightNewValue.Value));
            }

            if (request.WaterLeftNewValue.HasValue && roomL != null)
            {
                var oldReading = (await _ewRepo.GetLatestReadingBeforePeriodAsync(roomL.Id, "Water", period.TargetMonth, period.TargetYear))?.CurrentValue ?? 0;
                items.Add(EvaluateReading(roomL.Id, roomL.RoomNumber, "Water", oldReading, request.WaterLeftNewValue.Value));
            }

            if (request.WaterRightNewValue.HasValue && roomR != null && roomR.Id != roomL?.Id)
            {
                var oldReading = (await _ewRepo.GetLatestReadingBeforePeriodAsync(roomR.Id, "Water", period.TargetMonth, period.TargetYear))?.CurrentValue ?? 0;
                items.Add(EvaluateReading(roomR.Id, roomR.RoomNumber, "Water", oldReading, request.WaterRightNewValue.Value));
            }

            return new ManualPreviewResponseDto
            {
                HasErrors = items.Any(x => x.Status == "Error"),
                Results = items
            };
        }

        // ── M3: Confirm Manual Reading ──────────────────────────
        public async Task<ManualConfirmResponseDto> ConfirmManualReadingAsync(int guardUserId, ManualConfirmRequestDto request)
        {
            var period = await _ewRepo.GetBillingPeriodByIdAsync(request.BillingPeriodId)
                ?? throw new KeyNotFoundException($"Không tìm thấy kỳ thanh toán id = {request.BillingPeriodId}");

            if (period.Status != "Open")
                throw new InvalidOperationException($"Kỳ thanh toán Tháng {period.TargetMonth}/{period.TargetYear} hiện không mở ({period.Status})");

            var rooms = await _ewRepo.GetRoomsByBlockCodeAsync(request.BlockCode);
            if (!rooms.Any())
                throw new KeyNotFoundException($"Không tìm thấy phòng tương ứng mã block '{request.BlockCode}'");

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

                bool isFlagged = false;
                string? anomalyReason = null;

                if (newVal.Value < oldVal)
                {
                    isFlagged = true;
                    anomalyReason = $"Chỉ số mới ({newVal.Value}) nhỏ hơn chỉ số cũ ({oldVal})";
                }
                else if (newVal.Value == oldVal)
                {
                    isFlagged = true;
                    anomalyReason = "Mức tiêu thụ bằng 0, nghi vấn bỏ sót hoặc đồng hồ kẹt";
                }

                reading.Status = isFlagged ? "Flagged" : "Recorded";

                if (isNew)
                {
                    await _ewRepo.AddMeterReadingAsync(reading);
                }

                await _ewRepo.SaveChangesAsync();

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

            return response;
        }

        public async Task<bool> UpdateReadingAsync(int id, decimal currentValue, string? note)
        {
            var reading = await _ewRepo.GetReadingByIdAsync(id)
                ?? throw new KeyNotFoundException($"Không tìm thấy chỉ số id = {id}");

            if (currentValue < 0)
                throw new ArgumentException("Chỉ số không được là số âm");

            reading.CurrentValue = currentValue;
            reading.Note = note;

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
                reading.Status = "Recorded";
            }

            await _ewRepo.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteReadingAsync(int id)
        {
            var reading = await _ewRepo.GetReadingByIdAsync(id)
                ?? throw new KeyNotFoundException($"Không tìm thấy chỉ số id = {id}");

            _ewRepo.DeleteReading(reading);
            await _ewRepo.SaveChangesAsync();
            return true;
        }

        // ── I1, I2, I3, I4: Excel Import ────────────────────────
        public async Task<ExcelPreviewResponseDto> PreviewExcelImportAsync(Stream fileStream, int billingPeriodId)
        {
            var period = await _ewRepo.GetBillingPeriodByIdAsync(billingPeriodId)
                ?? throw new KeyNotFoundException($"Không tìm thấy kỳ thanh toán id = {billingPeriodId}");

            var parsedRows = EwExcelParser.ParseExcelFile(fileStream);
            var existingReadings = await _ewRepo.GetReadingsByPeriodAsync(billingPeriodId);

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
                if (!rooms.Any())
                {
                    row.Status = "Error";
                    row.ErrorMessage = $"Không tìm thấy phòng tương ứng cho block '{row.BlockCode}'";
                    errorCount++;
                    continue;
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

                // Kiểm tra cảnh báo bất thường
                bool hasWarning = false;
                var warningMsgs = new List<string>();

                if (row.LeftValue.HasValue && roomL != null)
                {
                    if (row.LeftValue.Value < row.LeftOldReading)
                    {
                        hasWarning = true;
                        warningMsgs.Add($"Phòng {row.LeftRoomNumber}: chỉ số mới ({row.LeftValue.Value}) < cũ ({row.LeftOldReading})");
                    }
                    else if (row.LeftValue.Value == row.LeftOldReading)
                    {
                        hasWarning = true;
                        warningMsgs.Add($"Phòng {row.LeftRoomNumber}: tiêu thụ bằng 0");
                    }
                }

                if (row.RightValue.HasValue && row.RightRoomId.HasValue)
                {
                    if (row.RightValue.Value < row.RightOldReading)
                    {
                        hasWarning = true;
                        warningMsgs.Add($"Phòng {row.RightRoomNumber}: chỉ số mới ({row.RightValue.Value}) < cũ ({row.RightOldReading})");
                    }
                    else if (row.RightValue.Value == row.RightOldReading)
                    {
                        hasWarning = true;
                        warningMsgs.Add($"Phòng {row.RightRoomNumber}: tiêu thụ bằng 0");
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
                AlreadyHasDataInPeriod = existingReadings.Any(),
                Rows = parsedRows
            };
        }

        public async Task<ExcelConfirmImportResponseDto> ConfirmExcelImportAsync(int guardUserId, ExcelConfirmImportRequestDto request)
        {
            var period = await _ewRepo.GetBillingPeriodByIdAsync(request.BillingPeriodId)
                ?? throw new KeyNotFoundException($"Không tìm thấy kỳ thanh toán id = {request.BillingPeriodId}");

            if (period.Status != "Open")
                throw new InvalidOperationException($"Kỳ thanh toán Tháng {period.TargetMonth}/{period.TargetYear} hiện không mở ({period.Status})");

            var blocks = await _ewRepo.GetAllBlocksAsync();
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

                // Cặp phòng L và R
                var pairs = new List<(int? RoomId, string RoomNumber, decimal? NewVal, decimal OldVal)>
                {
                    (row.LeftRoomId, row.LeftRoomNumber, row.LeftValue, row.LeftOldReading),
                    (row.RightRoomId, row.RightRoomNumber, row.RightValue, row.RightOldReading)
                };

                foreach (var p in pairs)
                {
                    if (!p.RoomId.HasValue || !p.NewVal.HasValue) continue;

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

                    bool isFlagged = false;
                    string? anomalyReason = null;

                    if (p.NewVal.Value < p.OldVal)
                    {
                        isFlagged = true;
                        anomalyReason = $"Chỉ số mới ({p.NewVal.Value}) nhỏ hơn chỉ số cũ ({p.OldVal})";
                    }
                    else if (p.NewVal.Value == p.OldVal)
                    {
                        isFlagged = true;
                        anomalyReason = "Mức tiêu thụ bằng 0, nghi vấn bỏ sót hoặc đồng hồ kẹt";
                    }

                    reading.Status = isFlagged ? "Flagged" : "Recorded";

                    if (existing == null)
                    {
                        await _ewRepo.AddMeterReadingAsync(reading);
                    }

                    await _ewRepo.SaveChangesAsync();

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

        public async Task<List<ImportBatchItemDto>> GetImportBatchesAsync(int? billingPeriodId)
        {
            var batches = await _ewRepo.GetImportBatchesAsync(billingPeriodId);
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

        public async Task<byte[]> GenerateExcelTemplateAsync(int? billingPeriodId, string? dormCode, string? meterType)
        {
            // 1. Xác định kỳ thanh toán
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

            // 2. Xác định ngày (ngày cuối cùng của tháng kỳ tính)
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

            // 3. Xác định loại đồng hồ cần sinh (E = Điện, W = Nước, mặc định sinh CẢ HAI)
            var typesToGenerate = new List<string>();
            if (string.IsNullOrWhiteSpace(meterType) ||
                meterType.Equals("ALL", StringComparison.OrdinalIgnoreCase) ||
                meterType.Equals("BOTH", StringComparison.OrdinalIgnoreCase) ||
                meterType.Equals("EW", StringComparison.OrdinalIgnoreCase))
            {
                // Mặc định tạo đủ cả dòng Điện (E) và dòng Nước (W) cho từng block
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

            // 4. Xác định mã KTX (Dorm)
            string dorm = "A";
            if (!string.IsNullOrWhiteSpace(dormCode))
            {
                dorm = dormCode.Trim().ToUpper();
                if (dorm.StartsWith("DOM-")) dorm = dorm.Substring(4);
                if (dorm.StartsWith("DORM-")) dorm = dorm.Substring(5);
            }

            // 5. Đọc danh sách Block trực tiếp từ Database (khớp 100% với bảng Blocks)
            var allBlocks = await _ewRepo.GetAllBlocksAsync();

            var filteredBlocks = allBlocks
                .Where(b => {
                    if (string.IsNullOrWhiteSpace(dormCode)) return true;
                    var dCode = b.Dorm?.Code ?? string.Empty;
                    var normDorm = dormCode.Trim().ToUpper();
                    return string.Equals(dCode, normDorm, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(dCode, "DOM-" + normDorm, StringComparison.OrdinalIgnoreCase)
                        || (normDorm.StartsWith("DOM-") && string.Equals(dCode, normDorm, StringComparison.OrdinalIgnoreCase));
                })
                .OrderBy(b => b.Code)
                .ToList();

            if (!filteredBlocks.Any())
            {
                filteredBlocks = allBlocks.OrderBy(b => b.Code).ToList();
            }

            // 6. Tạo danh sách các dòng template từ chính các Block trong DB (mỗi block có cả dòng Điện E và dòng Nước W)
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
                        Block = b.Code,   // Khớp 100% với bảng Blocks trong DB: "A1", "A2", "B1", "B2"
                        Type = t,         // "E" cho Điện, "W" cho Nước
                        Date = dateStr,
                        L = null,         // Để trống - Guard sẽ nhập chỉ số mới
                        R = null,         // Để trống - Guard sẽ nhập chỉ số mới
                        Term = termId
                    });
                }
            }

            return EwExcelParser.GenerateTemplateFromData(rows);
        }

        // ── Helpers ─────────────────────────────────────────────
        private static string GetBaseRoomCode(string roomNumber)
        {
            if (string.IsNullOrWhiteSpace(roomNumber)) return string.Empty;
            if (roomNumber.Length > 1 && (roomNumber.EndsWith("L", StringComparison.OrdinalIgnoreCase) || roomNumber.EndsWith("R", StringComparison.OrdinalIgnoreCase)))
            {
                return roomNumber.Substring(0, roomNumber.Length - 1);
            }
            return roomNumber;
        }

        private static ReadingPreviewItemDto EvaluateReading(int roomId, string roomNumber, string meterType, decimal oldReading, decimal newReading)
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

            if (newReading < oldReading)
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
                    FlagReason = $"Chỉ số mới ({newReading}) nhỏ hơn chỉ số cũ ({oldReading})"
                };
            }

            if (consumption == 0)
            {
                return new ReadingPreviewItemDto
                {
                    RoomId = roomId,
                    RoomNumber = roomNumber,
                    MeterType = meterType,
                    OldReading = oldReading,
                    NewReading = newReading,
                    Consumption = 0,
                    Status = "Warning",
                    Flagged = true,
                    FlagReason = "Mức tiêu thụ bằng 0, nghi vấn bỏ sót hoặc đồng hồ kẹt"
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
    }
}
