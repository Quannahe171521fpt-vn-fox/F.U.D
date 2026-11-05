using System;

namespace FUD.Common;

/// <summary>Lỗi nghiệp vụ: người dùng không có quyền thực hiện thao tác (HTTP 403).</summary>
public class ForbiddenException : Exception
{
    public ForbiddenException(string message) : base(message)
    {
    }
}
