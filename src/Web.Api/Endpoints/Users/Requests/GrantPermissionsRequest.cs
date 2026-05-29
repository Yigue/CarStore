using FluentValidation;

namespace Web.Api.Endpoints.Users.Requests;

public sealed record GrantPermissionsRequest(
    IEnumerable<string> Permissions
);

public sealed class GrantPermissionsRequestValidator : AbstractValidator<GrantPermissionsRequest>
{
    private static readonly string[] ValidPermissions =
    {
        "CanManageUsers",
        "CanManageRoles",
        "CanManageInventory",
        "CanManageSales",
        "CanManageFinance",
        "CanManageLeads",
        "CanViewReports"
    };

    public GrantPermissionsRequestValidator()
    {
        RuleFor(x => x.Permissions)
            .NotNull()
            .WithMessage("Permissions list is required");

        RuleForEach(x => x.Permissions)
            .Must(permission => ValidPermissions.Contains(permission))
            .WithMessage($"Permission must be one of: {string.Join(", ", ValidPermissions)}");
    }
}