using FluentValidation;

namespace Application.Platform.AuditLogs.GetPlatformAuditLogs;

internal sealed class GetPlatformAuditLogsQueryValidator : AbstractValidator<GetPlatformAuditLogsQuery>
{
    public GetPlatformAuditLogsQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);

        RuleFor(x => x)
            .Must(x => !x.FromUtc.HasValue || !x.ToUtc.HasValue || x.FromUtc <= x.ToUtc)
            .WithMessage("FromUtc must be less than or equal to ToUtc.");
    }
}
