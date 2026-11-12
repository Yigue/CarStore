using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Application.Common;
using Domain.Branches;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Branches;

/// <summary>sfc C2.1 / RN-048: adds a branch to the caller's dealer.</summary>
public sealed record CreateBranchCommand(string Name) : ICommand<Guid>;

/// <summary>sfc C2.1: the caller's branches, main first.</summary>
public sealed record GetBranchesQuery : IQuery<IReadOnlyList<BranchResponse>>;

public sealed record BranchResponse(Guid Id, string Name, bool IsMain);

internal sealed class CreateBranchCommandHandler(IApplicationDbContext context, ICurrentTenantService tenantService)
    : ICommandHandler<CreateBranchCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateBranchCommand command, CancellationToken cancellationToken)
    {
        Result guard = TenantGuard.EnsureHasTenant(tenantService);
        if (guard.IsFailure)
            return Result.Failure<Guid>(guard.Error);

        Guid dealerId = tenantService.DealerId;
        bool first = !await context.Branches.AnyAsync(b => b.DealerId == dealerId, cancellationToken);
        Result<Branch> created = Branch.Create(dealerId, command.Name, isMain: first);
        if (created.IsFailure)
            return Result.Failure<Guid>(created.Error);

        string normalized = created.Value.NameNormalized;
        if (await context.Branches.AnyAsync(b => b.DealerId == dealerId && b.NameNormalized == normalized, cancellationToken))
            return Result.Failure<Guid>(BranchErrors.AlreadyExists(created.Value.Name));

        context.Branches.Add(created.Value);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Result.Failure<Guid>(BranchErrors.AlreadyExists(created.Value.Name));
        }

        return Result.Success(created.Value.Id);
    }
}

internal sealed class GetBranchesQueryHandler(IApplicationDbContext context, ICurrentTenantService tenantService)
    : IQueryHandler<GetBranchesQuery, IReadOnlyList<BranchResponse>>
{
    public async Task<Result<IReadOnlyList<BranchResponse>>> Handle(GetBranchesQuery query, CancellationToken cancellationToken)
    {
        Result guard = TenantGuard.EnsureHasTenant(tenantService);
        if (guard.IsFailure)
            return Result.Failure<IReadOnlyList<BranchResponse>>(guard.Error);

        Guid dealerId = tenantService.DealerId;
        IReadOnlyList<BranchResponse> rows = await context.Branches.AsNoTracking()
            .Where(b => b.DealerId == dealerId)
            .OrderByDescending(b => b.IsMain).ThenBy(b => b.Name)
            .Select(b => new BranchResponse(b.Id, b.Name, b.IsMain))
            .ToListAsync(cancellationToken);
        return Result.Success(rows);
    }
}
