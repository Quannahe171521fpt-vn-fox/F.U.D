using System;
using System.Linq;
using FUD.DTOs.Account;
using FUD.Models;
using FluentValidation;

namespace FUD.Validators.Account;

/// <summary>Validate cho PUT /api/admin/users/{id}/roles — Cấp / thu hồi vai trò.</summary>
public class AssignRolesValidator : AbstractValidator<AssignRolesDto>
{
    public AssignRolesValidator()
    {
        RuleFor(x => x.Roles)
            .NotNull().WithMessage("Danh sách vai trò là bắt buộc")
            .Must(r => r != null && r.All(AccountConstants.Roles.All.Contains))
            .WithMessage("Vai trò chỉ được chọn trong danh sách: Student, Manager, Guard, Admin")
            .Must(r => r != null && r.Distinct(StringComparer.OrdinalIgnoreCase).Count() == r.Count)
            .WithMessage("Danh sách vai trò không được trùng lặp");
    }
}
