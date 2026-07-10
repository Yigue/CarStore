using Application.Abstractions.Messaging;

namespace Application.Dealers.Provision;

/// <summary>
/// Atomically provisions a new dealer tenant (DealerSettings row) and its first Admin user.
/// The two writes are wrapped in a single EF Core transaction; on any failure both rows roll back.
/// </summary>
public sealed record ProvisionDealerCommand(
    string DealerName,
    string Subdomain,
    string AdminEmail,
    string AdminPassword,
    string AdminFirstName,
    string AdminLastName) : ICommand<ProvisionDealerResponse>;