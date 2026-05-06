using Application.Abstractions.Messaging;

namespace Application.Queries.Users.GetAll;

public sealed class GetAllUsersQuery : IQuery<IEnumerable<UserResponse>>;