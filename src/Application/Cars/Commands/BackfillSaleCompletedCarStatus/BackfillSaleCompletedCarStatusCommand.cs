using Application.Abstractions.Messaging;

namespace Application.Cars.Commands.BackfillSaleCompletedCarStatus;

/// <summary>
/// Admin command for backfilling car status (<c>service_car</c>) to <c>Vendido</c> for cars with completed sales (qa-p1-integridad D5).
/// </summary>
/// <param name="DryRun">When <c>true</c>, compute and audit, but never persist changes to <c>cars</c>.</param>
/// <param name="Confirmed">Positive consent required for an apply. Ignored when <paramref name="DryRun"/> is <c>true</c>.</param>
public sealed record BackfillSaleCompletedCarStatusCommand(
    bool DryRun,
    bool Confirmed) : ICommand<BackfillSaleCompletedCarStatusResult>;
