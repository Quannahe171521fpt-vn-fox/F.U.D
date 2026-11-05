using FUD.DTOs.Account;
using FluentValidation;

namespace FUD.Validators.Account;

/// <summary>Validate cho POST /api/admin/users/{id}/restore — Khôi phục tài khoản.</summary>
public class RestoreAccountValidator : AbstractValidator<RestoreAccountDto>
{
    public RestoreAccountValidator()
    {
        RuleFor(x => x.Reason)
            .MaximumLength(500).WithMessage("Lý do khôi phục không vượt quá 500 ký tự");
    }
}
