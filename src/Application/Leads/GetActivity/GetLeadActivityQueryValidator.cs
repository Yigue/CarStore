using FluentValidation;

namespace Application.Leads.GetActivity;

internal sealed class GetLeadActivityQueryValidator : AbstractValidator<GetLeadActivityQuery>
{
    public GetLeadActivityQueryValidator()
    {
        RuleFor(q => q.LeadId).NotEmpty();
        RuleFor(q => q.Page).GreaterThanOrEqualTo(1);
        RuleFor(q => q.PageSize).InclusiveBetween(1, 200);
    }
}
