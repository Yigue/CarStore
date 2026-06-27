using Application.Clients.GetAll;
using Domain.Clients;
using Domain.Sales.Attributes;

namespace Application.Clients.Projections;

/// <summary>
/// Maps a materialized <see cref="Client"/> (with <c>Sales</c> loaded) into a
/// <see cref="ClientResponse"/>. Use the in-memory mapper for any query that
/// needs computed aggregations on the <c>Sales</c> collection — EF Core 8 cannot
/// translate the nested <c>Select … ToList()</c> form into SQL.
/// </summary>
internal static class ClientResponseMapper
{
    public static ClientResponse Map(Client client)
    {
        var completedSales = client.Sales
            .Where(s => s.Status == SaleStatus.Completed)
            .ToList();

        decimal total = completedSales.Sum(s => s.FinalPrice.Amount);
        var purchaseHistory = completedSales
            .OrderByDescending(s => s.SaleDate)
            .Select(s => new PurchaseHistoryEntry(s.Id, s.SaleDate, s.FinalPrice.Amount))
            .ToList();
        DateTime? lastPurchaseDate = completedSales.Count > 0
            ? completedSales.Max(s => s.SaleDate)
            : null;

        return new ClientResponse(
            client.Id,
            client.DealerId,
            client.Type,
            client.Status,
            client.FirstName,
            client.LastName,
            $"{client.FirstName} {client.LastName}".Trim(),
            client.Email.Value,
            client.Phone,
            null,
            client.DNI,
            client.City,
            client.Address,
            client.ZipCode,
            client.Notes,
            client.AcquisitionSource?.ToString(),
            client.AssignedAgentId,
            total,
            purchaseHistory,
            lastPurchaseDate,
            client.CreatedAt,
            client.UpdateAt,
            client.IsDeleted);
    }
}