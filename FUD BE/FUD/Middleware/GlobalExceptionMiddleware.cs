using System.Net;
using System.Text.Json;
using FUD.Common;

namespace FUD.Middleware
{
    /// <summary>
    /// Global error handling: chuyển exception thành response thống nhất.
    /// 400 validation, 401 unauthorized, 403 forbidden, 404 not found, 409 conflict.
    /// </summary>
    public class GlobalExceptionMiddleware
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;

        public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (ValidationException ex)
            {
                await WriteAsync(context, HttpStatusCode.BadRequest, ex.Message, ex.Errors);
            }
            catch (ForbiddenException ex)
            {
                await WriteAsync(context, HttpStatusCode.Forbidden, ex.Message);
            }
            catch (NotFoundException ex)
            {
                await WriteAsync(context, HttpStatusCode.NotFound, ex.Message);
            }
            catch (ConflictException ex)
            {
                await WriteAsync(context, HttpStatusCode.Conflict, ex.Message);
            }
            catch (KeyNotFoundException ex)
            {
                await WriteAsync(context, HttpStatusCode.NotFound, ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                await WriteAsync(context, HttpStatusCode.Conflict, ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception tại {Path}", context.Request.Path);
                await WriteAsync(context, HttpStatusCode.InternalServerError, "Đã xảy ra lỗi hệ thống, vui lòng thử lại");
            }
        }

        private static async Task WriteAsync(
            HttpContext context,
            HttpStatusCode statusCode,
            string message,
            List<string>? errors = null)
        {
            if (context.Response.HasStarted)
                return;

            context.Response.Clear();
            context.Response.StatusCode = (int)statusCode;
            context.Response.ContentType = "application/json; charset=utf-8";

            var payload = ApiResponse.Fail(message, errors);
            await context.Response.WriteAsync(JsonSerializer.Serialize(payload, JsonOptions));
        }
    }
}
