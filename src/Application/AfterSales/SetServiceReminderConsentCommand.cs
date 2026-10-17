using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Application.Common;
using Domain.Clients;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.AfterSales;

/// <summary>sfc C2.4 / RN-047: records whether the client accepts service reminders (opt-in, false by default).</summary>
public sealed record SetServiceReminderConsentCommand(Guid ClientId, bool Accepts) : ICommand;

internal sealed class SetServiceReminderConsentCommandHandler(IApplicationDbContext context, ICurrentTenantService tenantService)
    : ICommandHandler<SetServiceReminderConsentCommand>
{
    public async Task<Result> Handle(SetServiceReminderConsentCommand command, CancellationToken cancellationToken)
    {
        Result guard = TenantGuard.EnsureHasTenant(tenantService);
        if (guard.IsFailure)
            return guard;

        Client? client = await context.Clients.FirstOrDefaultAsync(c => c.Id == command.ClientId, cancellationToken);
        if (client is null || client.DealerId != tenantService.DealerId)
            return Result.Failure(ClientErrors.NotFound(command.ClientId));

        client.SetServiceReminderConsent(command.Accepts);
        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
