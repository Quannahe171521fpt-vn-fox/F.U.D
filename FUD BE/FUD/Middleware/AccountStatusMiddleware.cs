using System.Net;
using System.Security.Claims;
using System.Text.Json;
using FUD.Common;
using FUD.Models;
using Microsoft.EntityFrameworkCore;

namespace FUD.Middleware
{
    /// <summary>
    /// Chặn ngay trong pipeline các tài khoản không được phép truy cập:
    /// tài khoản bị Suspended / Pending, hoặc token đã cũ (token version không khớp).
    /// Áp dụng cho cả luồng đăng nhập thường và luồng SSO (Google) vì cả hai
    /// đều phát access token có chứa user id + token version.
    /// </summary>
    public class AccountStatusMiddleware
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private readonly RequestDelegate _next;
        private readonly ILogger<AccountStatusMiddleware> _logger;

        public AccountStatusMiddleware(RequestDelegate next, ILogger<AccountStatusMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var user = context.User;

            if (user.Identity?.IsAuthenticated == true)
            {
                var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                                 ?? user.FindFirst("sub")?.Value;

                if (int.TryParse(userIdClaim, out var userId))
                {
                    using var scope = context.RequestServices.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<FudContext>();

                    var account = await db.Users
                        .AsNoTracking()
                        .Where(u => u.Id == userId)
                        .Select(u => new { u.Id, u.Username, u.AccountStatus, u.TokenVersion })
                        .FirstOrDefaultAsync(context.RequestAborted);

                    if (account == null)
                    {
                        await WriteAsync(context, HttpStatusCode.Unauthorized,
                            "Tài khoản không tồn tại hoặc đã bị vô hiệu hóa");
                        return;
                    }

                    if (account.AccountStatus != AccountConstants.StatusActive)
                    {
                        _logger.LogWarning("Chặn truy cập cho user {UserId} với trạng thái {Status}",
                            account.Id, account.AccountStatus);

                        await WriteAsync(context, HttpStatusCode.Forbidden,
                            account.AccountStatus == AccountConstants.StatusSuspended
                                ? "Tài khoản đã bị tạm ngưng. Vui lòng liên hệ quản trị viên."
                                : "Tài khoản đang ở trạng thái chờ kích hoạt.");
                        return;
                    }

                    var tokenVersionClaim = user.FindFirst("token_version")?.Value;
                    if (!string.IsNullOrEmpty(tokenVersionClaim)
                        && int.TryParse(tokenVersionClaim, out var tokenVersion)
                        && tokenVersion != account.TokenVersion)
                    {
                        await WriteAsync(context, HttpStatusCode.Unauthorized,
                            "Phiên đăng nhập đã hết hạn, vui lòng đăng nhập lại");
                        return;
                    }
                }
            }

            await _next(context);
        }

        private static async Task WriteAsync(HttpContext context, HttpStatusCode statusCode, string message)
        {
            if (context.Response.HasStarted)
                return;

            context.Response.Clear();
            context.Response.StatusCode = (int)statusCode;
            context.Response.ContentType = "application/json; charset=utf-8";

            await context.Response.WriteAsync(JsonSerializer.Serialize(ApiResponse.Fail(message), JsonOptions));
        }
    }
}
