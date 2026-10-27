namespace FUD.Common
{
    /// <summary>Không tìm thấy tài nguyên → HTTP 404</summary>
    public class NotFoundException : Exception
    {
        public NotFoundException(string message) : base(message) { }
    }

    /// <summary>Xung đột trạng thái / trùng dữ liệu → HTTP 409</summary>
    public class ConflictException : Exception
    {
        public ConflictException(string message) : base(message) { }
    }

    /// <summary>Không đủ quyền → HTTP 403</summary>
    public class ForbiddenException : Exception
    {
        public ForbiddenException(string message) : base(message) { }
    }

    /// <summary>Dữ liệu không hợp lệ → HTTP 400</summary>
    public class ValidationException : Exception
    {
        public List<string> Errors { get; }

        public ValidationException(string message, IEnumerable<string>? errors = null)
            : base(message)
        {
            Errors = errors?.ToList() ?? new List<string> { message };
        }
    }
}
