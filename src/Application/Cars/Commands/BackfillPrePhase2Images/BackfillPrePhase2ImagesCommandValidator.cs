using FluentValidation;

namespace Application.Cars.Commands.BackfillPrePhase2Images;

public sealed class BackfillPrePhase2ImagesCommandValidator
    : AbstractValidator<BackfillPrePhase2ImagesCommand>
{
    public BackfillPrePhase2ImagesCommandValidator()
    {
        // An apply must be confirmed. We don't accept "dry run = false, confirmed = false"
        // because that combination is an ambiguous "I want to apply but I haven't said yes".
        RuleFor(x => x)
            .Must(cmd => cmd.DryRun || cmd.Confirmed)
            .WithMessage("Backfill apply requires Confirmed=true. Use DryRun=true to preview without changes.");
    }
}
