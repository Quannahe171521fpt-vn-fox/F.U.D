using FUD.Common;
using FUD.DTOs.Admin.Account;
using FUD.Services.Interfaces.Accounts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FUD.Controllers.Admin
{
    /// <summary>
    /// Quản lý tài khoản — chỉ dành cho Admin.
    /// Lớp bảo mật thật sự nằm ở [Authorize(Roles = "Admin")] + AccountStatusMiddleware.
    /// </summary>
    [Route("api/admin/users")]
    [ApiController]
    [Authorize(Roles = AccountConstants.RoleAdmin)]
    public class AdminUsersController : ControllerBase
    {
        private readonly IAccountService _accountService;

        public AdminUsersController(IAccountService accountService)
        {
            _accountService = accountService;
        }

        /// <summary>Danh sách tài khoản (tìm kiếm, lọc status/role, phân trang).</summary>
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] AccountQueryDto query)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse.Fail("Tham số không hợp lệ",
                    ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()));

            var result = await _accountService.GetAllAsync(query);
            return Ok(ApiResponse<object>.Ok(result));
        }

        /// <summary>Danh sách vai trò hệ thống có thể cấp.</summary>
        [HttpGet("roles")]
        public async Task<IActionResult> GetRoles()
        {
            var roles = await _accountService.GetRoleOptionsAsync();
            return Ok(ApiResponse<object>.Ok(roles));
        }

        /// <summary>Chi tiết tài khoản kèm lịch sử.</summary>
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var account = await _accountService.GetByIdAsync(id);
            if (account == null)
                return NotFound(ApiResponse.Fail($"Tài khoản {id} không tồn tại"));

            return Ok(ApiResponse<object>.Ok(account));
        }

        /// <summary>Tạo tài khoản mới.</summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateAccountDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse.Fail("Dữ liệu không hợp lệ",
                    ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()));

            var adminId = GetCurrentAdminId();
            var created = await _accountService.CreateAsync(dto, adminId);

            return CreatedAtAction(nameof(GetById), new { id = created.Id },
                ApiResponse<object>.Ok(created, "Tạo tài khoản thành công"));
        }

        /// <summary>Cấp / đổi / thu hồi vai trò.</summary>
        [HttpPut("{id:int}/roles")]
        public async Task<IActionResult> AssignRoles(int id, [FromBody] AssignRolesDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse.Fail("Dữ liệu không hợp lệ",
                    ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()));

            var adminId = GetCurrentAdminId();
            var updated = await _accountService.AssignRolesAsync(id, dto, adminId);

            return Ok(ApiResponse<object>.Ok(updated, "Cập nhật vai trò thành công"));
        }

        /// <summary>Tạm ngưng tài khoản (soft deactivate).</summary>
        [HttpPost("{id:int}/suspend")]
        public async Task<IActionResult> Suspend(int id, [FromBody] SuspendAccountDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse.Fail("Dữ liệu không hợp lệ",
                    ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()));

            var adminId = GetCurrentAdminId();
            var result = await _accountService.SuspendAsync(id, dto, adminId);

            return Ok(ApiResponse<object>.Ok(result, result.Message));
        }

        /// <summary>Khôi phục tài khoản đang tạm ngưng.</summary>
        [HttpPost("{id:int}/restore")]
        public async Task<IActionResult> Restore(int id, [FromBody] RestoreAccountDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse.Fail("Dữ liệu không hợp lệ",
                    ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()));

            var adminId = GetCurrentAdminId();
            var result = await _accountService.RestoreAsync(id, dto, adminId);

            return Ok(ApiResponse<object>.Ok(result, result.Message));
        }

        /// <summary>Lấy id của Admin đang đăng nhập từ claim NameIdentifier.</summary>
        private int GetCurrentAdminId()
        {
            var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                        ?? User.FindFirst("sub")?.Value;

            if (!int.TryParse(claim, out var id))
                throw new ForbiddenException("Không xác định được người dùng đang đăng nhập");

            return id;
        }
    }
}
