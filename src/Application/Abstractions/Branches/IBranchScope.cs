namespace Application.Abstractions.Branches;

/// <summary>
/// sfc C2.2 / RN-048 / ADR C26: the branch data scope of the current user. Null means "not restricted" (admins and any
/// role without <c>branch:scoped</c>); otherwise only those branches are visible, possibly none.
/// </summary>
public interface IBranchScope
{
    Task<IReadOnlyCollection<Guid>?> GetRestrictedBranchIdsAsync(CancellationToken cancellationToken);
}
