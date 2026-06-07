using Application.Abstractions.Messaging;

namespace Application.Cars.Commands.BackfillPrePhase2Images;

/// <summary>
/// Admin command for backfilling legacy <c>car_images</c> rows (REQ-FVIP-1).
/// <para>
/// Two flags, mutually exclusive in spirit: <paramref name="dryRun"/> returns what
/// <em>would</em> change without touching the rows; <paramref name="confirmed"/> is the
/// positive consent to mutate. The validator rejects <c>DryRun=false &amp;&amp; Confirmed=false</c>
/// so the handler never has to interpret that ambiguous state.
/// </para>
/// </summary>
/// <param name="DryRun">When <c>true</c>, compute and audit, but never persist changes to <c>car_images</c>.</param>
/// <param name="Confirmed">Positive consent required for an apply. Ignored when <paramref name="DryRun"/> is <c>true</c>.</param>
public sealed record BackfillPrePhase2ImagesCommand(
    bool DryRun,
    bool Confirmed) : ICommand<BackfillPrePhase2ImagesResult>;
