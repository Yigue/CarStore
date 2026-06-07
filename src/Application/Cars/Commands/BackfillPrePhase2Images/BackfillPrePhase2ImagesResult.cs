using System.Text.Json.Serialization;
using Domain.Cars;
using SharedKernel;

namespace Application.Cars.Commands.BackfillPrePhase2Images;

/// <summary>
/// Result of the pre-Phase-2 image backfill command. Returned for both <c>DryRun</c> and <c>Apply</c>
/// paths; the action discriminator is exposed via <see cref="Action"/>.
/// </summary>
public sealed record BackfillPrePhase2ImagesResult(
    Guid AuditId,
    BackfillAction Action,
    int AffectedRowCount,
    IReadOnlyList<Guid> AffectedImageIds);
