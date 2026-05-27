using FluentValidation;

namespace Application.Cars.Commands.AddReconditioningTask;

public sealed class AddReconditioningTaskCommandValidator : AbstractValidator<AddReconditioningTaskCommand>
{
    public AddReconditioningTaskCommandValidator()
    {
        RuleFor(x => x.CarId).NotEmpty();
        RuleFor(x => x.Description).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Cost).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Currency).NotEmpty().Length(3);
    }
}
