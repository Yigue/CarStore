using FluentValidation;

namespace Application.Roles.Update;

internal sealed class UpdateRoleCommandValidator : AbstractValidator<UpdateRoleCommand>
{
    public UpdateRoleCommandValidator()
    {
        RuleFor(x => x.RoleId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(60);
        RuleFor(x => x.Description).MaximumLength(200);
        RuleFor(x => x.Permissions)
            .NotEmpty()
            .WithMessage("A role needs at least one permission — otherwise its users can do nothing.");
    }
}
