namespace FUD.Infrastructure.AI
{
    public class MeterOcrRawResult
    {
        public bool IsMeterImage { get; set; }
        public string DetectedMeterType { get; set; } = "Unknown"; // Electricity | Water | Unknown
        public string? IntegerDigits { get; set; }
        public string? DecimalDigits { get; set; }
        public decimal? ReadingValue { get; set; }
        public string? RawDigits { get; set; }
        public double Confidence { get; set; } // 0.0 -> 1.0
        public string? Notes { get; set; }
    }

    public interface IMeterOcrService
    {
        Task<MeterOcrRawResult> ExtractMeterReadingAsync(
            byte[] imageBytes,
            string mimeType,
            string? expectedMeterType = null,
            CancellationToken cancellationToken = default);
    }
}
