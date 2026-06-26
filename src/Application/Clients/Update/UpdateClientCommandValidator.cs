using Domain.Clients.Attributes;
using FluentValidation;

namespace Application.Clients.Update;

public sealed class UpdateClientCommandValidator : AbstractValidator<UpdateClientCommand>
{
    public UpdateClientCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty();

        RuleFor(x => x.FirstName)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.LastName)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.DNI)
            .NotEmpty()
            .MaximumLength(20);

        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(255);

        RuleFor(x => x.Phone)
            .NotEmpty()
            .MaximumLength(20);

        RuleFor(x => x.Address)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Status)
            .IsInEnum();

        RuleFor(x => x.Type)
            .IsInEnum()
            .WithMessage("El campo 'type' es obligatorio.");
    }
}
