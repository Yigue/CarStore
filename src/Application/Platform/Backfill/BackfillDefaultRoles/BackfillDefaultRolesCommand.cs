using Application.Abstractions.Messaging;

namespace Application.Platform.Backfill.BackfillDefaultRoles;

/// <summary>
/// spr PR 4.1 (SPR-18, D6): platform-admin backfill that converges every existing dealer's
/// Admin/Empleado/Cliente roles onto <see cref="Domain.Users.DefaultRoleCatalog"/>.
/// </summary>
/// <param name="DryRun">
/// When <c>true</c>, computes and audits the per-dealer differences but never persists them.
/// When <c>false</c>, creates missing roles and syncs existing ones to match the catalogue
/// exactly (unlike seed-time behavior, this also removes permissions the catalogue no longer
/// lists — a full sync, not an additive merge).
/// </param>
/// <param name="ActorUserId">The platform admin triggering the run, recorded on the audit rows.</param>
public sealed record BackfillDefaultRolesCommand(
    bool DryRun,
    Guid ActorUserId) : ICommand<BackfillDefaultRolesResult>;
