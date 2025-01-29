using FluentValidation;

namespace Application.Cars.Delete;

internal sealed class DeleteCarCommandValidator : AbstractValidator<DeleteCarCommand>
{
    public DeleteCarCommandValidator()
    {
        RuleFor(c => c.CarId).NotEmpty();
    }
}
