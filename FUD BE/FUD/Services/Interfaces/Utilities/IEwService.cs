using FUD.DTOs.Utilities;
using Microsoft.AspNetCore.Http;

namespace FUD.Services.Interfaces.Utilities
{
    public interface IEwService
    {
        // ── UC13 Usages view sẵn có ─────────────────────────────
        Task<IEnumerable<object>> GetUsagesAsync(string? blockCode, string? meterType, int? month, int? year, int? termId = null, int? billingPeriodId = null, int? dormId = null, int? userId = null, bool isManager = false);

        // ── L1 & L2: Lookup & Progress ──────────────────────────
        Task<List<BillingPeriodLookupDto>> GetBillingPeriodsAsync();
        Task<BlockProgressResponseDto> GetBlockProgressAsync(int billingPeriodId, int? userId = null, bool isManager = false);

        // ── M1, M2, M3: Manual Entry & AI Photo Scan ────────────
        Task<BlockFormDataResponseDto> GetBlockFormDataAsync(string blockCode, int billingPeriodId, int? userId = null, bool isManager = false);
        Task<ManualPreviewResponseDto> PreviewManualReadingAsync(ManualPreviewRequestDto request, int? userId = null, bool isManager = false);
        Task<ManualConfirmResponseDto> ConfirmManualReadingAsync(int guardUserId, ManualConfirmRequestDto request, bool isManager = false);
        Task<MeterPhotoScanResponseDto> ScanMeterPhotoAsync(
            IFormFile photo,
            string? expectedMeterType,
            decimal? previousValue,
            bool saveEvidence = true,
            CancellationToken cancellationToken = default);
        Task<bool> UpdateReadingAsync(int id, decimal currentValue, string? note, int userId, bool isManager = false);
        Task<bool> DeleteReadingAsync(int id, int userId, bool isManager = false);

        // ── I1, I2, I3, I4: Excel Import ────────────────────────
        Task<ExcelPreviewResponseDto> PreviewExcelImportAsync(Stream fileStream, int billingPeriodId, int? userId = null, bool isManager = false);
        Task<ExcelConfirmImportResponseDto> ConfirmExcelImportAsync(int guardUserId, ExcelConfirmImportRequestDto request, bool isManager = false);
        Task<List<ImportBatchItemDto>> GetImportBatchesAsync(int? billingPeriodId, int? userId = null, bool isManager = false);
        Task<byte[]> GenerateExcelTemplateAsync(int? billingPeriodId, string? dormCode, string? meterType, int? userId = null, bool isManager = false);
        byte[] GenerateExcelTemplate();

        // ── A1, A2: Anomalies & Manager Adjust/Confirm ──────────
        Task<List<UsageAnomalyItemDto>> GetAnomaliesAsync(int? billingPeriodId, string? status);
        Task<ResolveAnomalyResponseDto> ResolveAnomalyAsync(int anomalyId, ResolveAnomalyRequestDto request, int managerUserId);

        // ── B1: Surcharge Calculation & Debt Notification ───────
        Task<BillingCalculationResultDto> CalculatePeriodSurchargesAsync(int billingPeriodId);

        // ── Student EW Usage ────────────────────────────────────
        Task<List<StudentTermOptionDto>> GetStudentTermsAsync(int studentUserId);
        Task<StudentEwReportDto?> GetStudentEwReportAsync(int studentUserId, int? termId);
    }
}
