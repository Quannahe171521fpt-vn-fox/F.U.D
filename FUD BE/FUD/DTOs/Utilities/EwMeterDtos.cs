namespace FUD.DTOs.Utilities
{
    // ═══════════════════════════════════════════════════════════
    // L1 — Lookup Billing Periods
    // ═══════════════════════════════════════════════════════════
    public class BillingPeriodLookupDto
    {
        public int Id { get; set; }
        public int TermId { get; set; }
        public int TargetMonth { get; set; }
        public int TargetYear { get; set; }
        public string DisplayName { get; set; } = string.Empty;   // "Tháng 9/2026"
        public string Status { get; set; } = string.Empty;        // Open | Calculated
        public string TermName { get; set; } = string.Empty;
    }

    // ═══════════════════════════════════════════════════════════
    // L2 — Block Progress trong kỳ
    // ═══════════════════════════════════════════════════════════
    public class BlockProgressResponseDto
    {
        public int BillingPeriodId { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public int TotalBlocks { get; set; }
        public int CompletedBlocks { get; set; }
        public int MissingBlocks { get; set; }
        public List<BlockProgressItemDto> Blocks { get; set; } = new();
    }

    public class BlockProgressItemDto
    {
        public string BlockCode { get; set; } = string.Empty;  // "A101"
        public string BlockName { get; set; } = string.Empty;
        public string DormName { get; set; } = string.Empty;
        public RoomReadingStatusDto? ElectricityLeft { get; set; }
        public RoomReadingStatusDto? ElectricityRight { get; set; }
        public RoomReadingStatusDto? WaterLeft { get; set; }
        public RoomReadingStatusDto? WaterRight { get; set; }
    }

    public class RoomReadingStatusDto
    {
        public int RoomId { get; set; }
        public string RoomNumber { get; set; } = string.Empty;  // "A101L"
        public bool HasReading { get; set; }
        public decimal? CurrentValue { get; set; }
        public string? RecordedAt { get; set; }   // dd/MM/yyyy
        public string? Status { get; set; }        // Recorded | Flagged | …
    }

    // ═══════════════════════════════════════════════════════════
    // M1 — Block Form Data (old readings, read-only)
    // ═══════════════════════════════════════════════════════════
    public class BlockFormDataResponseDto
    {
        public string BlockCode { get; set; } = string.Empty;
        public int BillingPeriodId { get; set; }
        public string DisplayPeriod { get; set; } = string.Empty;
        public RoomOldReadingDto? ElectricityLeft { get; set; }
        public RoomOldReadingDto? ElectricityRight { get; set; }
        public RoomOldReadingDto? WaterLeft { get; set; }
        public RoomOldReadingDto? WaterRight { get; set; }
    }

    public class RoomOldReadingDto
    {
        public int RoomId { get; set; }
        public string RoomNumber { get; set; } = string.Empty;
        public decimal OldReading { get; set; }       // PreviousValue để hiển thị read-only
        public bool AlreadyRecorded { get; set; }     // đã có chỉ số kỳ này chưa
        public decimal? ExistingCurrentValue { get; set; }
    }

    public class BlockFormDataQueryDto
    {
        public string BlockCode { get; set; } = string.Empty;
        public int BillingPeriodId { get; set; }
    }

    // ═══════════════════════════════════════════════════════════
    // M2 — Manual Preview Request & Result
    // ═══════════════════════════════════════════════════════════
    public class ManualPreviewRequestDto
    {
        public int BillingPeriodId { get; set; }
        public string BlockCode { get; set; } = string.Empty;
        public decimal? ElectricityLeftNewValue { get; set; }
        public decimal? ElectricityRightNewValue { get; set; }
        public decimal? WaterLeftNewValue { get; set; }
        public decimal? WaterRightNewValue { get; set; }
    }

    public class ManualPreviewResponseDto
    {
        public bool HasErrors { get; set; }
        public List<ReadingPreviewItemDto> Results { get; set; } = new();
    }

    public class ReadingPreviewItemDto
    {
        public int RoomId { get; set; }
        public string RoomNumber { get; set; } = string.Empty;   // "A101L"
        public string MeterType { get; set; } = string.Empty;   // Electricity | Water
        public decimal OldReading { get; set; }
        public decimal NewReading { get; set; }
        public decimal Consumption { get; set; }
        public string Status { get; set; } = "Valid";            // Valid | Warning | Error | Flagged
        public bool Flagged { get; set; } = false;
        public string? FlagReason { get; set; }
        public string? ErrorMessage { get; set; }
    }

    // ═══════════════════════════════════════════════════════════
    // M3 — Manual Confirm Request & Result
    // ═══════════════════════════════════════════════════════════
    public class ManualConfirmRequestDto
    {
        public int BillingPeriodId { get; set; }
        public string BlockCode { get; set; } = string.Empty;
        public decimal? ElectricityLeftNewValue { get; set; }
        public decimal? ElectricityRightNewValue { get; set; }
        public decimal? WaterLeftNewValue { get; set; }
        public decimal? WaterRightNewValue { get; set; }
        public string? Note { get; set; }
        /// <summary>true = bỏ qua error rows; false = reject nếu có error</summary>
        public bool ImportOnlyValid { get; set; } = false;
    }

    public class ManualConfirmResponseDto
    {
        public int Saved { get; set; }
        public int Flagged { get; set; }
        public int Skipped { get; set; }
        public bool ManagerAlerted { get; set; }
        public List<ReadingConfirmResultDto> Results { get; set; } = new();
    }

    public class ReadingConfirmResultDto
    {
        public int? ReadingId { get; set; }
        public string RoomNumber { get; set; } = string.Empty;
        public string MeterType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;       // Recorded | Flagged | Skipped
        public string? AnomalyReason { get; set; }
    }

    // ═══════════════════════════════════════════════════════════
    // I1, I2, I3 — Excel Import DTOs
    // ═══════════════════════════════════════════════════════════
    public class ExcelPreviewRowDto
    {
        public int RowIndex { get; set; }
        public string Dorm { get; set; } = string.Empty;         // "A"
        public string BlockCode { get; set; } = string.Empty;    // "A101"
        public string MeterType { get; set; } = string.Empty;    // "Electricity" | "Water"
        public string DateStr { get; set; } = string.Empty;      // "28/02/2026"
        public decimal? LeftValue { get; set; }
        public decimal? RightValue { get; set; }
        public int? LeftRoomId { get; set; }
        public int? RightRoomId { get; set; }
        public string LeftRoomNumber { get; set; } = string.Empty;
        public string RightRoomNumber { get; set; } = string.Empty;
        public decimal LeftOldReading { get; set; }
        public decimal RightOldReading { get; set; }
        public decimal LeftConsumption { get; set; }
        public decimal RightConsumption { get; set; }
        public string Status { get; set; } = "Valid";            // Valid | Warning | Error
        public string? ErrorMessage { get; set; }
        public string? FlagReason { get; set; }
    }

    public class ExcelPreviewResponseDto
    {
        public int TotalRows { get; set; }
        public int ValidRows { get; set; }
        public int WarningRows { get; set; }
        public int ErrorRows { get; set; }
        public bool AlreadyHasDataInPeriod { get; set; }
        public List<ExcelPreviewRowDto> Rows { get; set; } = new();
    }

    public class ExcelConfirmImportRequestDto
    {
        public int BillingPeriodId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public bool ImportOnlyValid { get; set; } = true;
        public bool OverwriteExisting { get; set; } = true;
        public List<ExcelPreviewRowDto> Rows { get; set; } = new();
    }

    public class ExcelConfirmImportResponseDto
    {
        public int BatchId { get; set; }
        public DateTime ImportTime { get; set; }
        public int TotalRows { get; set; }
        public int SavedReadings { get; set; }
        public int FlaggedReadings { get; set; }
        public int ErrorRows { get; set; }
        public bool ManagerAlerted { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class ImportBatchItemDto
    {
        public int Id { get; set; }
        public int BillingPeriodId { get; set; }
        public string BillingPeriodDisplay { get; set; } = string.Empty;
        public string BlockCode { get; set; } = string.Empty;
        public string GuardName { get; set; } = string.Empty;
        public string Method { get; set; } = string.Empty;
        public string? FileName { get; set; }
        public int TotalRows { get; set; }
        public int ErrorRows { get; set; }
        public DateTime ImportTime { get; set; }
    }

    // ═══════════════════════════════════════════════════════════
    // Excel Template Model (Template xuất toàn bộ block)
    // ═══════════════════════════════════════════════════════════
    public class ExcelTemplateRowModel
    {
        public string Dorm { get; set; } = "A";
        public string Block { get; set; } = string.Empty;
        public string Type { get; set; } = "W";
        public string Date { get; set; } = string.Empty;
        public decimal? L { get; set; }
        public decimal? R { get; set; }
        public int Term { get; set; } = 1;
    }
}
