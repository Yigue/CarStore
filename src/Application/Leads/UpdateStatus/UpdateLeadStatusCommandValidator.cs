using FluentValidation;

namespace Application.Leads.UpdateStatus;

public sealed class UpdateLeadStatusCommandValidator : AbstractValidator<UpdateLeadStatusCommand>
{
    public UpdateLeadStatusCommandValidator()
    {
        RuleFor(x => x.LeadId).NotEmpty();
        // qa-p1-integridad D2: an omitted newStatus must be rejected explicitly, not silently
        // treated as LeadStatus.Nuevo (member 0). IsInEnum() short-circuits to valid on null,
        // so NotNull is the rule that actually rejects the omission.
        RuleFor(x => x.NewStatus).NotNull().WithMessage("newStatus is required.");
        RuleFor(x => x.NewStatus).IsInEnum();
    }
}
