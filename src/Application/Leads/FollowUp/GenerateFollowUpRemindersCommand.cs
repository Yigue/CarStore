using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Leads;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Leads.FollowUp;

/// <summary>
/// sfc B6.2 / RN-041: the daily follow-up sweep, run by <c>FollowUpCadenceJob</c> with no tenant. Returns how many
/// reminders were created.
/// </summary>
public sealed record GenerateFollowUpRemindersCommand : ICommand<int>;

/// <summary>
/// An open lead (Nuevo..Negociación) whose last activity is older than its dealer's cadence gets one
/// <see cref="LeadActivityType.FollowUpReminder"/> for the assigned salesperson. While that reminder is the latest thing
/// that happened to the lead, no other one is added. Every query is per dealer or per lead, never mixing tenants.
/// </summary>
internal sealed class GenerateFollowUpRemindersCommandHandler(IApplicationDbContext context, IDateTimeProvider dateTimeProvider)
    : ICommandHandler<GenerateFollowUpRemindersCommand, int>
{
    private static readonly LeadStatus[] OpenStatuses =
        [LeadStatus.Nuevo, LeadStatus.Contactado, LeadStatus.Demostracion, LeadStatus.Negociacion];

    public async Task<Result<int>> Handle(GenerateFollowUpRemindersCommand command, CancellationToken cancellationToken)
    {
        DateTime now = dateTimeProvider.UtcNow;
        var cadences = await context.DealerSettings
            .Where(d => d.FollowUpCadenceDays > 0)
            .Select(d => new { d.DealerId, d.FollowUpCadenceDays })
            .ToListAsync(cancellationToken);

        int created = 0;
        foreach (var dealer in cadences)
        {
            DateTime quietSince = now.AddDays(-dealer.FollowUpCadenceDays);
            var leads = await context.Leads
                .Where(l => l.DealerId == dealer.DealerId && OpenStatuses.Contains(l.Status))
                .Select(l => new { l.Id, l.CreatedAt, l.AssignedAgentId })
                .ToListAsync(cancellationToken);

            foreach (var lead in leads)
            {
                var last = await context.LeadActivities
                    .Where(a => a.LeadId == lead.Id)
                    .OrderByDescending(a => a.OccurredAtUtc)
                    .Select(a => new { a.OccurredAtUtc, a.Type })
                    .FirstOrDefaultAsync(cancellationToken);

                if (last?.Type == LeadActivityType.FollowUpReminder)
                    continue;

                DateTime lastActivity = last?.OccurredAtUtc ?? lead.CreatedAt;
                if (lastActivity > quietSince)
                    continue;

                int quietDays = (now - lastActivity).Days;
                context.LeadActivities.Add(LeadActivity.Record(
                    dealer.DealerId,
                    lead.Id,
                    LeadActivityType.FollowUpReminder,
                    $"Seguimiento pendiente: sin actividad hace {quietDays} días",
                    now,
                    relatedEntityId: lead.AssignedAgentId,
                    relatedEntityType: lead.AssignedAgentId is null ? null : "User"));
                created++;
            }
        }

        await context.SaveChangesAsync(cancellationToken);
        return Result.Success(created);
    }
}
