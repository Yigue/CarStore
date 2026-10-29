using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Application.Billing.Entitlements;
using Domain.Shared.ValueObjects;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SharedKernel;

namespace Application.Users.Register;

internal sealed class RegisterUserCommandHandler(
    IApplicationDbContext context,
    DbContext dbContext,
    IPasswordHasher passwordHasher,
    ICurrentTenantService tenantService,
    Application.Legal.LegalAcceptanceRecorder legalRecorder,
    IEntitlementService entitlementService)
    : ICommandHandler<RegisterUserCommand, Guid>
{
    public async Task<Result<Guid>> Handle(RegisterUserCommand command, CancellationToken cancellationToken)
    {
        Result termsCheck = legalRecorder.EnsureCurrentVersion(command.AcceptedTermsVersion);
        if (termsCheck.IsFailure)
        {
            return Result.Failure<Guid>(termsCheck.Error);
        }

        var email = new Email(command.Email);

        if (await context.Users.IgnoreQueryFilters().AnyAsync(u => (string)u.Email == email.Value, cancellationToken))
        {
            return Result.Failure<Guid>(UserErrors.EmailNotUnique);
        }

        var dealerId = tenantService.DealerId;
        if (dealerId == Guid.Empty)
        {
            return Result.Failure<Guid>(UserErrors.TenantRequired);
        }

        // Find or create "Cliente" role for this dealer idempotently.
        // spr PR 4.1 (SPR-18, D6): permission set comes from DefaultRoleCatalog, the single
        // source of truth — not an inline list.
        var clienteRole = await context.Roles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.DealerId == dealerId && r.Name == DefaultRoleCatalog.Cliente, cancellationToken);

        bool clienteRoleIsNew = clienteRole is null;
        if (clienteRole is null)
        {
            clienteRole = DefaultRoleCatalog.BuildRole(dealerId, DefaultRoleCatalog.Cliente);
            context.Roles.Add(clienteRole);
        }

        // Security review (sfc A3.1), fourth round: self-registration never checks the plan's seat
        // limit, which is correct ONLY because the role it assigns is guaranteed to have zero
        // permissions. A newly-built role from DefaultRoleCatalog always starts that way, but an
        // EXISTING role found by name could have gained permissions since — e.g. a dealer renamed a
        // staff role to literally "Cliente" (UpdateRoleCommandHandler does not forbid that). If that
        // ever happens, this is the one unauthenticated path that would otherwise create unlimited
        // staff-seat-consuming users with no entitlement check at all.
        // Fifth round: the lock must be acquired BEFORE checking RoleConsumesASeatAsync, not after —
        // same TOCTOU this exact check already closed for AssignRole/UpdateUser/GrantDealerMembership
        // in the fourth round. A newly-built role needs no lock at all: it isn't persisted yet, so no
        // concurrent UpdateRoleCommandHandler call could possibly be racing to grant it a permission.
        IDbContextTransaction? transaction = !clienteRoleIsNew && dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;

        User user;

        try
        {
            if (!clienteRoleIsNew)
            {
                await entitlementService.AcquireUsersLockAsync(dealerId, cancellationToken);

                bool existingClienteRoleConsumesASeat = await entitlementService.RoleConsumesASeatAsync(clienteRole.Id, cancellationToken);
                if (existingClienteRoleConsumesASeat)
                {
                    Result entitlementCheck = await entitlementService.EnsureCanAddAsync(
                        dealerId, PlanResource.Users, cancellationToken);
                    if (entitlementCheck.IsFailure)
                    {
                        if (transaction is not null)
                        {
                            await transaction.RollbackAsync(cancellationToken);
                        }

                        return Result.Failure<Guid>(entitlementCheck.Error);
                    }
                }
            }

            user = new User(
                dealerId,
                command.Email,
                command.FirstName,
                command.LastName,
                passwordHasher.Hash(command.Password),
                clienteRole.Id);

            context.Users.Add(user);

            legalRecorder.RecordTermsAcceptance(dealerId, user.Id, command.AcceptanceIp, command.AcceptanceUserAgent);

            await context.SaveChangesAsync(cancellationToken);

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
        }
        catch
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }

            throw;
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }

        return Result.Success(user.Id);
    }
}
