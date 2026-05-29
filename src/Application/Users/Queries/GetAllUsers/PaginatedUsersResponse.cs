namespace Application.Users.Queries.GetAllUsers;

public sealed record PaginatedUsersResponse(
    IEnumerable<UserListResponse> Data,
    int Total,
    int Page,
    int PageSize
);