namespace FUD.Services.Keeping
{
    /// <summary>
    /// Không đủ điều kiện giữ phòng → Controller trả 422 kèm reasonCode ổn định cho Frontend.
    /// </summary>
    public class KeepingNotEligibleException : InvalidOperationException
    {
        /// <summary>Mã lý do ổn định (xem DTOs.Keeping.KeepingReasonCodes).</summary>
        public string ReasonCode { get; }

        public KeepingNotEligibleException(string reasonCode, string message) : base(message)
        {
            ReasonCode = reasonCode;
        }
    }

    /// <summary>
    /// Xung đột dữ liệu khi tạo giữ phòng (race condition) → Controller trả 409.
    /// </summary>
    public class KeepingConflictException : InvalidOperationException
    {
        public KeepingConflictException(string message) : base(message)
        {
        }
    }
}
