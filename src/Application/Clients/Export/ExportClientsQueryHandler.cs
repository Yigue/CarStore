using System.Text;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Common.Csv;
using Domain.Clients;
using Domain.Clients.Attributes;
using Domain.Sales.Attributes;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Clients.Export;

/// <summary>
/// Streams up to 10 000 clients into a UTF-8 BOM CSV byte array (ADR-4).
/// Returns 413 (via ExportErrors.LimitExceeded) if the filtered result set exceeds that cap.
/// </summary>
internal sealed class ExportClientsQueryHandler(IApplicationDbContext context)
    : IQueryHandler<ExportClientsQuery, byte[]>
{
    public async Task<Result<byte[]>> Handle(ExportClientsQuery query, CancellationToken cancellationToken)
    {
        var dbQuery = context.Clients
            .AsNoTracking()
            .Include(c => c.Sales)
            .AsQueryable();

        // Optional id filter
        if (query.Ids is { Count: > 0 })
            dbQuery = dbQuery.Where(c => query.Ids.Contains(c.Id));

        // Search / filter (mirrors GetAllClientsQueryHandler logic)
        if (!string.IsNullOrWhiteSpace(query.Status) && Enum.TryParse<ClientStatus>(query.Status, true, out var statusVal))
            dbQuery = dbQuery.Where(c => c.Status == statusVal);

        if (!string.IsNullOrWhiteSpace(query.Type) && Enum.TryParse<ClientType>(query.Type, true, out var typeVal))
            dbQuery = dbQuery.Where(c => c.Type == typeVal);

        if (!string.IsNullOrWhiteSpace(query.Source) && Enum.TryParse<AcquisitionSource>(query.Source, true, out var sourceVal))
            dbQuery = dbQuery.Where(c => c.AcquisitionSource == sourceVal);

        if (query.AssignedAgentId.HasValue)
            dbQuery = dbQuery.Where(c => c.AssignedAgentId == query.AssignedAgentId.Value);

        if (query.CreatedFrom.HasValue)
            dbQuery = dbQuery.Where(c => c.CreatedAt >= query.CreatedFrom.Value);

        if (query.CreatedTo.HasValue)
            dbQuery = dbQuery.Where(c => c.CreatedAt <= query.CreatedTo.Value);

        // Cap check before iteration
        int count = await dbQuery.CountAsync(cancellationToken);
        if (count > ExportErrors.MaxRows)
            return Result.Failure<byte[]>(ExportErrors.LimitExceeded(count));

        // Build CSV in memory
        using var ms = new MemoryStream();

        // Write UTF-8 BOM
        CsvRowWriter.WriteBom(ms);

        using var writer = new StreamWriter(ms, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true);

        // Header row
        await CsvRowWriter.WriteRowAsync(writer, new[]
        {
            "Id", "FirstName", "LastName", "Email", "Phone",
            "DocumentNumber", "City", "Address", "Status", "Type",
            "AcquisitionSource", "TotalSalesAmount", "CreatedAt"
        });

        // Data rows via async enumerable
        await foreach (var client in dbQuery
            .OrderBy(c => c.LastName).ThenBy(c => c.FirstName)
            .AsAsyncEnumerable().WithCancellation(cancellationToken))
        {
            var completedSalesTotal = client.Sales
                .Where(s => s.Status == SaleStatus.Completed)
                .Sum(s => s.FinalPrice.Amount);

            await CsvRowWriter.WriteRowAsync(writer, new[]
            {
                client.Id.ToString(),
                client.FirstName,
                client.LastName,
                client.Email.Value,
                client.Phone,
                client.DNI,
                client.City,
                client.Address,
                client.Status.ToString(),
                client.Type.ToString(),
                client.AcquisitionSource?.ToString(),
                completedSalesTotal.ToString("F2"),
                client.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ssZ")
            });
        }

        await writer.FlushAsync(cancellationToken);
        return Result.Success(ms.ToArray());
    }
}
