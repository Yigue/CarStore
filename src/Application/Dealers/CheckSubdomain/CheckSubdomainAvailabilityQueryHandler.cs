using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Common;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Dealers.CheckSubdomain;

/// <summary>
/// Resolves whether a subdomain slug is currently available to self-provision.
/// Always returns <see cref="Result{TResponse}.Success(TResponse)"/> — the
/// endpoint surfaces "not available" via the body, not an HTTP error.
/// </summary>
internal sealed class CheckSubdomainAvailabilityQueryHandler(
    IApplicationDbContext context)
    : IQueryHandler<CheckSubdomainAvailabilityQuery, SubdomainAvailabilityResponse>
{
    public async Task<Result<SubdomainAvailabilityResponse>> Handle(
        CheckSubdomainAvailabilityQuery query,
        CancellationToken cancellationToken)
    {
        var slug = (query.Subdomain ?? string.Empty).Trim().ToLowerInvariant();

        if (ReservedSubdomains.Reserved.Contains(slug))
        {
            return Result.Success(new SubdomainAvailabilityResponse(
                Available: false,
                Reason: "reserved",
                Reserved: true));
        }

        var existing = await context.DealerSettings
            .AsNoTracking()
            .AnyAsync(s => s.HostName == slug, cancellationToken);

        if (existing)
        {
            return Result.Success(new SubdomainAvailabilityResponse(
                Available: false,
                Reason: "taken",
                Reserved: false));
        }

        return Result.Success(new SubdomainAvailabilityResponse(
            Available: true,
            Reason: null,
            Reserved: false));
    }
}