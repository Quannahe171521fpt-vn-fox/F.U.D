namespace FUD.Common
{
    /// <summary>
    /// Hằng số dùng chung cho module Account Management.
    /// </summary>
    public static class AccountConstants
    {
        // ── Trạng thái tài khoản ──────────────────────────────────
        public const string StatusPending = "Pending";
        public const string StatusActive = "Active";
        public const string StatusSuspended = "Suspended";

        public static readonly string[] AllStatuses = { StatusPending, StatusActive, StatusSuspended };

        // ── Vai trò hệ thống ─────────────────────────────────────
        public const string RoleStudent = "Student";
        public const string RoleManager = "Manager";
        public const string RoleGuard = "Guard";
        public const string RoleAdmin = "Admin";

        public static readonly string[] AllRoles = { RoleStudent, RoleManager, RoleGuard, RoleAdmin };

        // ── Lý do tạm ngưng ──────────────────────────────────────
        public const string ReasonViolation = "Violation";
        public const string ReasonGraduation = "Graduation";
        public const string ReasonTermination = "Termination";
        public const string ReasonOther = "Other";

        public static readonly string[] AllSuspensionReasons =
            { ReasonViolation, ReasonGraduation, ReasonTermination, ReasonOther };

        // ── Trạng thái gửi thông báo ──────────────────────────────
        public const string NotificationNone = "None";
        public const string NotificationSent = "Sent";
        public const string NotificationFailed = "Failed";

        // ── Hành động đổi vai trò ────────────────────────────────
        public const string RoleActionGrant = "Grant";
        public const string RoleActionAdjust = "Adjust";
        public const string RoleActionRevoke = "Revoke";
    }
}
