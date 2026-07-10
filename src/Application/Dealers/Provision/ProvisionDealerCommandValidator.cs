using System.Text.RegularExpressions;
using Application.Common;
using Application.Dealers.Provision;
using FluentValidation;

namespace Application.Dealers.Provision;

/// <summary>
/// Validation rules for <see cref="ProvisionDealerCommand"/>.
/// Password policy is intentionally stricter (10 chars min) than the legacy
/// <c>RegisterUserCommandValidator</c> (8 chars) — provisioning is anonymous and
/// the first Admin is the highest-privilege user in a fresh tenant.
/// </summary>
internal sealed class ProvisionDealerCommandValidator : AbstractValidator<ProvisionDealerCommand>
{
    // Lowercase a-z, 0-9, hyphens; cannot start or end with a hyphen; 3–32 chars total.
    private static readonly Regex SubdomainPattern =
        new("^[a-z0-9](?:[a-z0-9-]{1,30}[a-z0-9])?$", RegexOptions.Compiled);

    public ProvisionDealerCommandValidator()
    {
        RuleFor(c => c.DealerName)
            .NotEmpty()
            .MinimumLength(2).WithMessage("Dealer name must be at least 2 characters.")
            .MaximumLength(200).WithMessage("Dealer name must be at most 200 characters.");

        RuleFor(c => c.Subdomain)
            .NotEmpty()
            .MinimumLength(3).WithMessage("Subdomain must be at least 3 characters.")
            .MaximumLength(32).WithMessage("Subdomain must be at most 32 characters.")
            .Matches(SubdomainPattern)
                .WithMessage("Subdomain must be lowercase a–z, 0–9, hyphens (no leading/trailing hyphen).")
            .Must(slug => !ReservedSubdomains.Reserved.Contains(slug))
                .WithMessage("That subdomain is reserved and cannot be provisioned.");

        RuleFor(c => c.AdminEmail)
            .NotEmpty()
            .EmailAddress().WithMessage("Admin email must be a valid email address.");

        RuleFor(c => c.AdminPassword)
            .NotEmpty()
            .MinimumLength(10).WithMessage("Password must be at least 10 characters long.")
            .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches("[a-z]").WithMessage("Password must contain at least one lowercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain at least one digit.")
            .Matches(@"[!@#$%^&*()_+\-=\[\]{};':""\\|,.<>\/?]")
                .WithMessage("Password must contain at least one special character.");

        RuleFor(c => c.AdminFirstName)
            .NotEmpty()
            .MaximumLength(100).WithMessage("First name must be at most 100 characters.");

        RuleFor(c => c.AdminLastName)
            .NotEmpty()
            .MaximumLength(100).WithMessage("Last name must be at most 100 characters.");
    }
}