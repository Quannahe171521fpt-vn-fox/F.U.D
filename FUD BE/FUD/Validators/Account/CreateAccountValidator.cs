using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using FUD.DTOs.Account;
using FluentValidation;

namespace FUD.Validators.Account;

/// <summary>Validate cho POST /api/admin/users — Tạo tài khoản.</summary>
public class CreateAccountValidator : AbstractValidator<CreateAccountDto>
{
    private static readonly Regex EmailRegex = new(
        @"^[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}$",
        RegexOptions.Compiled);

    public CreateAccountValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("Username là bắt buộc")
            .MaximumLength(50).WithMessage("Username không vượt quá 50 ký tự")
            .Matches(@"^[a-zA-Z0-9._\-]+$").WithMessage("Username chỉ chứa chữ, số và các ký tự . _ -");

        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Họ tên là bắt buộc")
            .MaximumLength(100).WithMessage("Họ tên không vượt quá 100 ký tự");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email là bắt buộc")
            .MaximumLength(100).WithMessage("Email không vượt quá 100 ký tự")
            .Must(e => EmailRegex.IsMatch(e ?? string.Empty)).WithMessage("Email không đúng định dạng");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Mật khẩu là bắt buộc")
            .MinimumLength(8).WithMessage("Mật khẩu phải có tối thiểu 8 ký tự")
            .Matches(@"[A-Z]").WithMessage("Mật khẩu phải có ít nhất 1 chữ hoa")
            .Matches(@"[a-z]").WithMessage("Mật khẩu phải có ít nhất 1 chữ thường")
            .Matches(@"[0-9]").WithMessage("Mật khẩu phải có ít nhất 1 chữ số");

        RuleFor(x => x.Phone)
            .MaximumLength(20).WithMessage("Số điện thoại không vượt quá 20 ký tự")
            .Matches(@"^[0-9+\-\s]*$").WithMessage("Số điện thoại không hợp lệ")
            .When(x => !string.IsNullOrWhiteSpace(x.Phone));

        RuleFor(x => x.Gender)
            .Must(g => g is null || g is "Male" or "Female")
            .WithMessage("Giới tính chỉ chấp nhận 'Male' hoặc 'Female'");

        RuleFor(x => x.Roles)
            .NotEmpty().WithMessage("Cần ít nhất một vai trò ban đầu")
            .Must(r => r != null && r.Count > 0).WithMessage("Cần ít nhất một vai trò ban đầu")
            .Must(r => r != null && r.All(Models.AccountConstants.Roles.All.Contains))
            .WithMessage("Vai trò chỉ được chọn trong danh sách: Student, Manager, Guard, Admin");

        RuleFor(x => x.Roles)
            .Must(r => r != null && r.Distinct(StringComparer.OrdinalIgnoreCase).Count() == r.Count)
            .WithMessage("Danh sách vai trò không được trùng lặp");
    }
}
