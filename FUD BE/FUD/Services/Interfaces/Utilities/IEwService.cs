using FUD.DTOs.Utilities;

namespace FUD.Services.Interfaces.Utilities
{
    public interface IEwService
    {
        // ── UC13 Usages view sẵn có ─────────────────────────────
        Task<IEnumerable<object>> GetUsagesAsync(string? blockCode, string? meterType, int? month, int? year, int? termId = null, int? billingPeriodId = null);

        // ── L1 & L2: Lookup & Progress ──────────────────────────
        Task<List<BillingPeriodLookupDto>> GetBillingPeriodsAsync();
        Task<BlockProgressResponseDto> GetBlockProgressAsync(int billingPeriodId);

        // ── M1, M2, M3: Manual Entry ────────────────────────────
        Task<BlockFormDataResponseDto> GetBlockFormDataAsync(string blockCode, int billingPeriodId);
        Task<ManualPreviewResponseDto> PreviewManualReadingAsync(ManualPreviewRequestDto request);
        Task<ManualConfirmResponseDto> ConfirmManualReadingAsync(int guardUserId, ManualConfirmRequestDto request);
        Task<bool> UpdateReadingAsync(int id, decimal currentValue, string? note);
        Task<bool> DeleteReadingAsync(int id);

        // ── I1, I2, I3, I4: Excel Import ────────────────────────
        Task<ExcelPreviewResponseDto> PreviewExcelImportAsync(Stream fileStream, int billingPeriodId);
        Task<ExcelConfirmImportResponseDto> ConfirmExcelImportAsync(int guardUserId, ExcelConfirmImportRequestDto request);
        Task<List<ImportBatchItemDto>> GetImportBatchesAsync(int? billingPeriodId);
        Task<byte[]> GenerateExcelTemplateAsync(int? billingPeriodId, string? dormCode, string? meterType);
        byte[] GenerateExcelTemplate();
    }
}
