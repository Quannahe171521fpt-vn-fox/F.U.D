using FUD.DTOs.Utilities;
using FUD.Models;

namespace FUD.Repositories.Interfaces.Utilities
{
    public interface IEwRepository
    {
        // ── UC13 Usages view sẵn có ─────────────────────────────
        Task<IEnumerable<object>> GetGroupedUsagesAsync(string? blockCode, string? meterType, int? month, int? year, int? termId = null, int? billingPeriodId = null, int? dormId = null);

        // ── Guards & Dorm Isolation ─────────────────────────────
        Task<Guard?> GetGuardByUserIdAsync(int userId);
        Task<int?> GetGuardDormIdAsync(int userId);
        Task<int?> GetValidManagerIdAsync(int userId);

        // ── Billing Periods ─────────────────────────────────────
        Task<List<BillingPeriod>> GetAllBillingPeriodsAsync();
        Task<List<BillingPeriod>> GetBillingPeriodsAsync();
        Task<BillingPeriod?> GetBillingPeriodByIdAsync(int id);

        // ── Blocks & Rooms ──────────────────────────────────────
        Task<List<Block>> GetAllBlocksAsync();
        Task<Block?> GetBlockByCodeAsync(string blockCode, int dormId);
        Task<List<Room>> GetAllActiveRoomsWithBlockAsync();
        Task<List<Room>> GetRoomsByBlockCodeAsync(string blockCode);

        // ── Meter Readings ──────────────────────────────────────
        Task<List<MeterReading>> GetReadingsByPeriodAsync(int billingPeriodId);
        Task<MeterReading?> GetReadingForPeriodAsync(int billingPeriodId, int roomId, string meterType);
        Task<MeterReading?> GetLatestReadingBeforePeriodAsync(int roomId, string meterType, int targetMonth, int targetYear);
        Task<decimal> GetLatestReadingValueAsync(int roomId, string meterType, int currentBillingPeriodId);
        Task AddMeterReadingAsync(MeterReading reading);
        Task<MeterReading> AddReadingAsync(MeterReading reading);
        Task<MeterReading?> GetReadingByIdAsync(int id);
        Task<MeterReading?> GetByIdAsync(int id);
        Task UpdateReadingAsync(MeterReading reading);
        void DeleteReading(MeterReading reading);

        // ── Import Batches ──────────────────────────────────────
        Task AddImportBatchAsync(ImportBatch batch);
        Task<List<ImportBatch>> GetImportBatchesAsync(int? billingPeriodId, int? dormId = null);
        Task<ImportBatch?> GetImportBatchByIdAsync(int id);

        // ── Anomalies & Manager Adjust/Confirm ──────────────────
        Task AddUsageAnomalyAsync(UsageAnomaly anomaly);
        Task AddAnomalyAsync(UsageAnomaly anomaly);
        Task<List<UsageAnomaly>> GetAnomaliesAsync(int? billingPeriodId, string? status);
        Task<UsageAnomaly?> GetAnomalyByIdAsync(int id);
        Task<List<UsageAnomaly>> GetActiveAnomaliesByReadingIdAsync(int readingId);
        Task<decimal> GetRoomTermConsumptionBeforePeriodAsync(int roomId, string meterType, int termId, int currentBillingPeriodId);

        // ── Surcharge Calculation & Equal Cost Splitting ────────
        Task<RoomSurchargeCalculationResult> CalculateAndPublishRoomSurchargeAsync(int billingPeriodId, int roomId);

        // ── Student EW Usage ────────────────────────────────────
        Task<List<StudentTermOptionDto>> GetStudentTermsAsync(int studentUserId);
        Task<StudentEwReportDto?> GetStudentEwReportAsync(int studentUserId, int? termId);

        Task SaveChangesAsync();
    }
}
