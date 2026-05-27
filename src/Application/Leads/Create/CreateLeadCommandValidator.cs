using FluentValidation;

namespace Application.Leads.Create;

public sealed class CreateLeadCommandValidator : AbstractValidator<CreateLeadCommand>
{
    public CreateLeadCommandValidator()
    {
        RuleFor(x => x.ClientName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Phone).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Notes).MaximumLength(2000).When(x => x.Notes is not null);
    }
}
