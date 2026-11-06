using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using FUD.Common;
using FUD.DTOs.Account;
using FUD.Services.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FUD.Controllers;

/// <summary>
/// API quản lý tài khoản cho Admin: Create Account, Assign Roles, Suspend Account, Restore Account.
/// </summary>
[Route("api/admin/users")]
[ApiController]
[Authorize(Policy = "RequireAdmin")]
public class AdminUsersController : ControllerBase
{
    private readonly IAccountService _accountService;
    private readonly IValidator<CreateAccountDto> _createValidator;
    private readonly IValidator<AssignRolesDto> _assignRolesValidator;
    private readonly IValidator<SuspendAccountDto> _suspendValidator;
    private readonly IValidator<RestoreAccountDto> _restoreValidator;

    public AdminUsersController(
        IAccountService accountService,
        IValidator<CreateAccountDto> createValidator,
        IValidator<AssignRolesDto> assignRolesValidator,
        IValidator<SuspendAccountDto> suspendValidator,
        IValidator<RestoreAccountDto> restoreValidator)
    {
        _accountService = accountService;
        _createValidator = createValidator;
        _assignRolesValidator = assignRolesValidator;
        _suspendValidator = suspendValidator;
        _restoreValidator = restoreValidator;
    }

    // ─────────────────────────────────────────────
    // GET /api/admin/users — Danh sách (tìm kiếm, lọc, phân trang)
    // ─────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] AccountQueryDto query)
    {
        var result = await _accountService.GetAllAsync(query);
        return Ok(ApiResponse<object>.Ok(result));
    }

    // ─────────────────────────────────────────────
    // GET /api/admin/users/{id} — Chi tiết
    // ─────────────────────────────────────────────
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var account = await _accountService.GetByIdAsync(id);
        if (account == null)
            return NotFound(ApiResponse.Fail($"Tài khoản id {id} không tồn tại"));

        return Ok(ApiResponse<object>.Ok(account));
    }

    // ─────────────────────────────────────────────
    // POST /api/admin/users — Create Account
    // ─────────────────────────────────────────────
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAccountDto dto)
    {
        var validation = await _createValidator.ValidateAsync(dto);
        if (!validation.IsValid)
            return BadRequest(ApiResponse.Fail("Dữ liệu không hợp lệ", validation.Errors.Select(e => e.ErrorMessage).ToList()));

        var adminId = GetCurrentAdminId();
        if (adminId == null)
            return Unauthorized(ApiResponse.Fail("Token không hợp lệ"));

        try
        {
            var created = await _accountService.CreateAsync(dto, adminId.Value);
            return CreatedAtAction(nameof(GetById), new { id = created.Id },
                ApiResponse<object>.Ok(created, "Tạo tài khoản thành công"));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ApiResponse.Fail(ex.Message));
        }
    }

    // ─────────────────────────────────────────────
    // PUT /api/admin/users/{id}/roles — Assign Roles
    // ─────────────────────────────────────────────
    [HttpPut("{id:int}/roles")]
    public async Task<IActionResult> AssignRoles(int id, [FromBody] AssignRolesDto dto)
    {
        var validation = await _assignRolesValidator.ValidateAsync(dto);
        if (!validation.IsValid)
            return BadRequest(ApiResponse.Fail("Dữ liệu không hợp lệ", validation.Errors.Select(e => e.ErrorMessage).ToList()));

        var adminId = GetCurrentAdminId();
        if (adminId == null)
            return Unauthorized(ApiResponse.Fail("Token không hợp lệ"));

        try
        {
            var updated = await _accountService.AssignRolesAsync(id, dto, adminId.Value);
            return Ok(ApiResponse<object>.Ok(updated, "Cập nhật vai trò thành công"));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
        catch (ForbiddenException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ApiResponse.Fail(ex.Message));
        }
    }

    // ─────────────────────────────────────────────
    // POST /api/admin/users/{id}/suspend — Suspend Account
    // ─────────────────────────────────────────────
    [HttpPost("{id:int}/suspend")]
    public async Task<IActionResult> Suspend(int id, [FromBody] SuspendAccountDto dto)
    {
        var validation = await _suspendValidator.ValidateAsync(dto);
        if (!validation.IsValid)
            return BadRequest(ApiResponse.Fail("Dữ liệu không hợp lệ", validation.Errors.Select(e => e.ErrorMessage).ToList()));

        var adminId = GetCurrentAdminId();
        if (adminId == null)
            return Unauthorized(ApiResponse.Fail("Token không hợp lệ"));

        try
        {
            var updated = await _accountService.SuspendAsync(id, dto, adminId.Value);
            return Ok(ApiResponse<object>.Ok(updated, "Tạm ngưng tài khoản thành công"));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
        catch (ForbiddenException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ApiResponse.Fail(ex.Message));
        }
    }

    // ─────────────────────────────────────────────
    // POST /api/admin/users/{id}/restore — Restore Account
    // ─────────────────────────────────────────────
    [HttpPost("{id:int}/restore")]
    public async Task<IActionResult> Restore(int id, [FromBody] RestoreAccountDto dto)
    {
        var validation = await _restoreValidator.ValidateAsync(dto);
        if (!validation.IsValid)
            return BadRequest(ApiResponse.Fail("Dữ liệu không hợp lệ", validation.Errors.Select(e => e.ErrorMessage).ToList()));

        var adminId = GetCurrentAdminId();
        if (adminId == null)
            return Unauthorized(ApiResponse.Fail("Token không hợp lệ"));

        try
        {
            var updated = await _accountService.RestoreAsync(id, dto, adminId.Value);
            return Ok(ApiResponse<object>.Ok(updated, "Khôi phục tài khoản thành công"));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ApiResponse.Fail(ex.Message));
        }
    }

    /// <summary>Lấy Id của Admin đang đăng nhập tời JWT.</summary>
    private int? GetCurrentAdminId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(value, out var id) ? id : null;
    }
}
