using Domain.Clients;
using Domain.Clients.Attributes;
using SharedKernel;

namespace Application.Queries.Clients.GetTop;

public sealed record ClientWithRevenueResponse(
    Guid Id,
    string FirstName,
    string LastName,
    string DNI,
    string Email,
    string Phone,
    string Address,
    ClientStatus Status,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    decimal TotalSalesAmount);