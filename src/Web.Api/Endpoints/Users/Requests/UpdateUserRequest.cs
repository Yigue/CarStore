using Application.Users.Commands.UpdateUser;
using Domain.Users;
using FluentValidation;

namespace Web.Api.Endpoints.Users.Requests;

public sealed record UpdateUserRequest(
    string FirstName,
    string LastName,
    string? Phone,
    string Role,
    bool IsActive
);

public sealed class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.LastName)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.Phone)
            .MaximumLength(20)
            .When(x => x.Phone is not null);

        RuleFor(x => x.Role)
            .Must(role => Enum.TryParse<UserRole>(role, true, out _))
            .WithMessage("Role must be a valid value: Admin, Empleado, Cliente, Invitado");
    }
}