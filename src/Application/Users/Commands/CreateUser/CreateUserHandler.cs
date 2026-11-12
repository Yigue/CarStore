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

namespace Application.Users.Commands.CreateUser;

internal sealed class CreateUserCommandHandler(
    IApplicationDbContext context,
    DbContext dbContext,
    IPasswordHasher passwordHasher,
    ICurrentTenantService tenantService,
    IEntitlementService entitlementService)
    : ICommandHandler<CreateUserCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateUserCommand command, CancellationToken cancellationToken)
    {
        var email = new Email(command.Email);

        // Security review (sfc A3.1): this is a sibling path to InviteUserCommandHandler that creates a
        // User row directly — it must honor the same plan seat limit, or the limit is trivially bypassed
        // by any caller with CanManageUsers. Same advisory-lock-guarded transaction pattern.
        IDbContextTransaction? transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;

        try
        {
            Result entitlementCheck = await entitlementService.EnsureCanAddAsync(
                tenantService.DealerId, PlanResource.Users, cancellationToken);
            if (entitlementCheck.IsFailure)
            {
                if (transaction is not null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                }

                return Result.Failure<Guid>(entitlementCheck.Error);
            }

            Result<Guid> result = await CreateAsync(command, email, cancellationToken);

            if (result.IsSuccess)
            {
                if (transaction is not null)
                {
                    await transaction.CommitAsync(cancellationToken);
                }
            }
            else if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }

            return result;
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
    }

    private async Task<Result<Guid>> CreateAsync(CreateUserCommand command, Email email, CancellationToken cancellationToken)
    {
        // `u.Email.Value.ToLower()` never translates against Postgres — EF Core throws
        // InvalidOperationException at LINQ-compile time, not a DomainException, so every call
        // fell through to the generic 500 handler regardless of role or any other input. Email's
        // own constructor already normalizes to lowercase, so the cast below is both the
        // translatable form (mirrors RegisterUserCommandHandler) and the only comparison needed.
        bool emailExists = await context.Users
            .IgnoreQueryFilters()
            .AnyAsync(u => (string)u.Email == email.Value && u.DealerId == tenantService.DealerId, cancellationToken);

        if (emailExists)
        {
            return Result.Failure<Guid>(UserErrors.EmailNotUnique);
        }

        var roleBelongsToDealer = await context.Roles
            .AnyAsync(r => r.Id == command.RoleId && r.DealerId == tenantService.DealerId, cancellationToken);

        if (!roleBelongsToDealer)
        {
            return Result.Failure<Guid>(RoleErrors.NotFound(command.RoleId));
        }

        var user = new User(
            tenantService.DealerId,
            email.Value,
            command.FirstName.Trim(),
            command.LastName.Trim(),
            passwordHasher.Hash(command.Password),
            command.RoleId);

        // Set phone if provided
        if (!string.IsNullOrWhiteSpace(command.Phone))
        {
            user.UpdatePhone(command.Phone.Trim());
        }

        context.Users.Add(user);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(user.Id);
    }
}
