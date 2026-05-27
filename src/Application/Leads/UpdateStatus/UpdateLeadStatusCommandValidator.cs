using FluentValidation;

namespace Application.Leads.UpdateStatus;

public sealed class UpdateLeadStatusCommandValidator : AbstractValidator<UpdateLeadStatusCommand>
{
    public UpdateLeadStatusCommandValidator()
    {
        RuleFor(x => x.LeadId).NotEmpty();
        RuleFor(x => x.NewStatus).IsInEnum();
    }
}
