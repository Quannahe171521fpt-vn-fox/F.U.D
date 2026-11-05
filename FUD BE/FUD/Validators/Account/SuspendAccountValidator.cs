using System;
using FUD.DTOs.Account;
using FUD.Models;
using FluentValidation;

namespace FUD.Validators.Account;

/// <summary>Validate cho POST /api/admin/users/{id}/suspend — Tạm ngưng tài khoản.</summary>
public class SuspendAccountValidator : AbstractValidator<SuspendAccountDto>
{
    public SuspendAccountValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Lý do tạm ngưng là bắt buộc")
            .Must(r => AccountConstants.SuspensionReasons.All.Contains(r?.Trim().ToLowerInvariant() ?? string.Empty))
            .WithMessage("Lý do chỉ chấp nhận: violation, graduation, termination, other");

        RuleFor(x => x.Notes)
            .MaximumLength(500).WithMessage("Ghi chú không vượt quá 500 ký tự");
    }
}
