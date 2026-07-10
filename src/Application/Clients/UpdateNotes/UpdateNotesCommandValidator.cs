using FluentValidation;

namespace Application.Clients.UpdateNotes;

public sealed class UpdateNotesCommandValidator : AbstractValidator<UpdateNotesCommand>
{
    public UpdateNotesCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty();

        RuleFor(x => x.Notes)
            .MaximumLength(2000)
            .WithMessage("Notes cannot exceed 2000 characters.")
            .When(x => x.Notes is not null);
    }
}
