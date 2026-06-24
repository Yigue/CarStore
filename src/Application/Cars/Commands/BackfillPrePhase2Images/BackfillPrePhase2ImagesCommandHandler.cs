using System.Diagnostics;
using System.Text.Json;
using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Domain.Cars;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Cars.Commands.BackfillPrePhase2Images;

/// <summary>
/// Backfills the <see cref="CarImage.IsCover"/> flag on legacy rows that lost their cover
/// after the migration to object storage. Spec: REQ-FVIP-1.
///
/// Per-car rule: if a <c>Car</c> has zero rows with <c>IsCover=true</c>, mark the first
/// remaining row (ordered by <c>DisplayOrder</c> ASC, then <c>Id</c> ASC) as
/// <c>IsCover=true</c>. Re-runnable: rows that already satisfy the condition are filtered out
/// at query time, so the second apply is a no-op (zero affected rows) but still appends an
/// audit row (the audit log is append-only by design).
///
/// Tenancy: scoped explicitly by <see cref="ICurrentTenantService.DealerId"/> via a join on
/// <c>Cars</c>. The global query filter on <c>Cars</c> provides a second line of defense;
/// <c>CarImage</c> itself is shared across tenants (catalog data).
/// </summary>
internal sealed class BackfillPrePhase2ImagesCommandHandler(
    IApplicationDbContext context,
    ICurrentTenantService tenantService,
    IUserContext userContext,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<BackfillPrePhase2ImagesCommand, BackfillPrePhase2ImagesResult>
{
    public async Task<Result<BackfillPrePhase2ImagesResult>> Handle(
        BackfillPrePhase2ImagesCommand command,
        CancellationToken cancellationToken)
    {
        if (!command.DryRun && !command.Confirmed)
        {
            // Defense in depth — the validator already rejects this, but the handler must
            // never silently treat it as a no-op apply.
            return Result.Failure<BackfillPrePhase2ImagesResult>(new Error(
                "Backfill.NotConfirmed",
                "Apply requires Confirmed=true. Use DryRun=true to preview.",
                ErrorType.Validation));
        }

        Guid dealerId = tenantService.DealerId;
        if (dealerId == Guid.Empty)
        {
            return Result.Failure<BackfillPrePhase2ImagesResult>(new Error(
                "Backfill.NoTenant",
                "Cannot run backfill without a resolved tenant.",
                ErrorType.Validation));
        }

        var stopwatch = Stopwatch.StartNew();
        var action = command.DryRun ? BackfillAction.DryRun : BackfillAction.Apply;

        // 1. Cars in the resolved tenant that have no row with IsCover=true yet.
        IQueryable<Guid> carsNeedingCover = context.Cars
            .Where(c => c.DealerId == dealerId && !c.Images.Any(i => i.IsCover))
            .Select(c => c.Id);

        // 2. Pull all images for those cars, then pick the first per car in memory.
        //    The (DisplayOrder, Id) ordering matches the pre-Phase-2 "primary" convention.
        //    Memory grouping is acceptable here: per-tenant candidate count is bounded
        //    by the number of cars without a cover, not by total images.
        List<CarImage> candidates = await (
            from img in context.CarImages.AsNoTracking()
            where carsNeedingCover.Contains(img.CarId)
            orderby img.CarId, img.DisplayOrder, img.Id
            select img)
            .ToListAsync(cancellationToken);

        List<CarImage> firstPerCar = candidates
            .GroupBy(img => img.CarId)
            .Select(g => g.First())
            .ToList();

        List<Guid> affectedImageIds = firstPerCar.Select(c => c.Id).ToList();
        int affectedCount = affectedImageIds.Count;

        // 3. Apply mutations (or skip entirely for dry-run).
        if (!command.DryRun && affectedCount > 0)
        {
            // Re-load the rows tracked so EF can issue UPDATE statements; the in-memory
            // candidates above were AsNoTracking.
            List<CarImage> tracked = await context.CarImages
                .Where(ci => affectedImageIds.Contains(ci.Id))
                .ToListAsync(cancellationToken);

            foreach (CarImage image in tracked)
            {
                image.SetAsCover(true);
            }

            await context.SaveChangesAsync(cancellationToken);
        }

        stopwatch.Stop();

        // 4. Append the audit row (always, even for dry-runs and zero-affected applies).
        //    The audit log is append-only by design — see BackfillAudit xmldoc.
        var metadata = JsonSerializer.Serialize(affectedImageIds);
        BackfillAudit audit = BackfillAudit.Create(
            dealerId,
            userContext.UserId,
            action,
            affectedCount,
            (int)stopwatch.ElapsedMilliseconds,
            metadata,
            dateTimeProvider.UtcNow);

        context.BackfillAudits.Add(audit);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(new BackfillPrePhase2ImagesResult(
            audit.Id,
            action,
            affectedCount,
            affectedImageIds));
    }
}
