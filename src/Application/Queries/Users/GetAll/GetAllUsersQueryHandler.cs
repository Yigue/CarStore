using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Queries.Users.GetAll;

internal sealed class GetAllUsersQueryHandler
    : IQueryHandler<GetAllUsersQuery, IEnumerable<UserResponse>>
{
    private readonly IApplicationDbContext _context;

    public GetAllUsersQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<IEnumerable<UserResponse>>> Handle(
        GetAllUsersQuery query,
        CancellationToken cancellationToken)
    {
        var users = await _context.Users
            .AsNoTracking()
            .OrderBy(u => u.LastName)
            .ThenBy(u => u.FirstName)
            .Select(u => new UserResponse(
                u.Id,
                u.Email.Value,
                u.FirstName,
                u.LastName,
                u.DealerId))
            .ToListAsync(cancellationToken);

        return Result.Success<IEnumerable<UserResponse>>(users);
    }
}