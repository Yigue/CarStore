using Application.Abstractions.Messaging;

namespace Application.Users.Queries.GetAgents;

/// <summary>
/// Returns the active staff users (Admin/Empleado) of the current tenant that are
/// eligible for assignment (e.g. as the "Vendedor" on a sale). Unlike
/// <see cref="Application.Users.Queries.GetAllUsers.GetAllUsersQuery"/>, this query
/// is gated by <c>sales:read</c> instead of <c>CanManageUsers</c>, so non-admin staff
/// can populate assignment selects without needing user-management permissions.
/// </summary>
public sealed record GetAgentsQuery : IQuery<List<AgentResponse>>;
