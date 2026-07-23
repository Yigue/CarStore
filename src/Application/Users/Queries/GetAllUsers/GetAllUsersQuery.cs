using Application.Abstractions.Messaging;
using Domain.Users;

namespace Application.Users.Queries.GetAllUsers;

public sealed record GetAllUsersQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    Guid? RoleId = null,
    bool? IsActive = null
) : IQuery<PaginatedUsersResponse>;