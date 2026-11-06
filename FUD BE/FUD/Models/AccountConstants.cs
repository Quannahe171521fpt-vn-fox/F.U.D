namespace FUD.Models;

/// <summary>Hằng số dùng chung cho module Account Management.</summary>
public static class AccountConstants
{
    /// <summary>Các trạng thái tài khoản hợp lệ.</summary>
    public static class Statuses
    {
        public const string Pending = "Pending";
        public const string Active = "Active";
        public const string Suspended = "Suspended";
    }

    /// <summary>Các trạng thái gửi thông báo.</summary>
    public static class NotificationStatus
    {
        public const string None = "None";
        public const string Sent = "Sent";
        public const string Failed = "Failed";
    }

    /// <summary>Các hành động thay đổi vai trò.</summary>
    public static class RoleActions
    {
        public const string Grant = "Grant";
        public const string Adjust = "Adjust";
        public const string Revoke = "Revoke";
    }

    /// <summary>Các lý do tạm ngưng tài khoản.</summary>
    public static class SuspensionReasons
    {
        public const string Violation = "violation";
        public const string Graduation = "graduation";
        public const string Termination = "termination";
        public const string Other = "other";

        public static readonly string[] All = { Violation, Graduation, Termination, Other };
    }

    /// <summary>Các vai trò hệ thống được phép gán.</summary>
    public static class Roles
    {
        public const string Student = "Student";
        public const string Manager = "Manager";
        public const string GuardRole = "Guard";
        public const string Admin = "Admin";

        public static readonly string[] All = { Student, Manager, GuardRole, Admin };
    }
}
