using System;
using System.Collections.Generic;
using Domain.Cars;

namespace Application.Cars.Commands.BackfillSaleCompletedCarStatus;

/// <summary>
/// Result of the sale-completed car status backfill command (qa-p1-integridad D5).
/// </summary>
public sealed record BackfillSaleCompletedCarStatusResult(
    Guid AuditId,
    BackfillAction Action,
    int AffectedRowCount,
    IReadOnlyList<Guid> AffectedCarIds);
