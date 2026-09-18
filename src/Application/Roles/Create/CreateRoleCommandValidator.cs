using FluentValidation;

namespace Application.Roles.Create;

internal sealed class CreateRoleCommandValidator : AbstractValidator<CreateRoleCommand>
{
    public CreateRoleCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(60);
        RuleFor(x => x.Description).MaximumLength(200);

        // A role with no permissions is a role that can do nothing. Refusing it here is kinder
        // than letting someone create it, assign three people, and discover the dashboard is
        // empty for all of them.
        RuleFor(x => x.Permissions)
            .NotEmpty()
            .WithMessage("A role needs at least one permission — otherwise its users can do nothing.");
    }
}
