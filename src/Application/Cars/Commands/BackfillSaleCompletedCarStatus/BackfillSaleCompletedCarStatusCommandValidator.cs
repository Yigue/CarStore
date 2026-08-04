using FluentValidation;

namespace Application.Cars.Commands.BackfillSaleCompletedCarStatus;

public sealed class BackfillSaleCompletedCarStatusCommandValidator
    : AbstractValidator<BackfillSaleCompletedCarStatusCommand>
{
    public BackfillSaleCompletedCarStatusCommandValidator()
    {
        RuleFor(x => x)
            .Must(cmd => cmd.DryRun || cmd.Confirmed)
            .WithMessage("Backfill apply requires Confirmed=true. Use DryRun=true to preview without changes.");
    }
}
