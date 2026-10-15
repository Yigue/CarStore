using System.Globalization;
using System.Text;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Application.Common;
using Domain.Cars;
using Domain.Shared.ValueObjects;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Reconditioning.Csv;

/// <summary>sfc B7.3 / RN-038: imports reconditioning tasks for one car from CSV (columns description, cost).</summary>
public sealed record ImportReconditioningCsvCommand(Guid CarId, string Content) : ICommand<ReconditioningImportResult>
{
    /// <summary>Spec default: 2 MB.</summary>
    public const int MaxBytes = 2 * 1024 * 1024;
}

public sealed record ReconditioningImportResult(int Created, IReadOnlyList<CsvRowError> Errors);

/// <param name="Row">1-based line number in the file, the header being row 1.</param>
public sealed record CsvRowError(int Row, string Message);

internal sealed class ImportReconditioningCsvCommandHandler(IApplicationDbContext context, ICurrentTenantService tenantService)
    : ICommandHandler<ImportReconditioningCsvCommand, ReconditioningImportResult>
{
    public async Task<Result<ReconditioningImportResult>> Handle(ImportReconditioningCsvCommand command, CancellationToken cancellationToken)
    {
        Result guard = TenantGuard.EnsureHasTenant(tenantService);
        if (guard.IsFailure)
            return Result.Failure<ReconditioningImportResult>(guard.Error);

        Car? car = await context.Cars.FirstOrDefaultAsync(c => c.Id == command.CarId, cancellationToken);
        if (car is null || car.DealerId != tenantService.DealerId)
            return Result.Failure<ReconditioningImportResult>(CarErrors.NotFound(command.CarId));

        if (Encoding.UTF8.GetByteCount(command.Content) > ImportReconditioningCsvCommand.MaxBytes)
            return Result.Failure<ReconditioningImportResult>(Error.Validation("CSV_TOO_LARGE", "El archivo supera el máximo de 2 MB."));

        List<List<string>> rows = SimpleCsv.Parse(command.Content);
        List<string> header = rows.Count > 0 ? rows[0].Select(h => h.Trim().ToLowerInvariant()).ToList() : [];
        int descriptionAt = header.IndexOf("description");
        int costAt = header.IndexOf("cost");
        if (descriptionAt < 0 || costAt < 0)
            return Result.Failure<ReconditioningImportResult>(Error.Validation(
                "INVALID_CSV_HEADERS", "El CSV debe tener las columnas 'description' y 'cost'."));

        CurrencyCode currency = CurrencyCode.Parse(car.Price.Currency);
        var errors = new List<CsvRowError>();
        int created = 0;
        for (int i = 1; i < rows.Count; i++)
        {
            int rowNumber = i + 1;
            List<string> row = rows[i];
            string description = row.ElementAtOrDefault(descriptionAt)?.Trim() ?? string.Empty;
            string costText = row.ElementAtOrDefault(costAt)?.Trim() ?? string.Empty;

            if (description.Length == 0)
            {
                errors.Add(new CsvRowError(rowNumber, $"Falta la descripción en la fila {rowNumber}"));
                continue;
            }

            if (!TryParseCost(costText, out decimal cost) || cost <= 0)
            {
                errors.Add(new CsvRowError(rowNumber, $"Costo inválido en la fila {rowNumber}"));
                continue;
            }

            // Added explicitly: the task carries its own Guid, so EF would otherwise take it for an existing row.
            context.ReconditioningTasks.Add(car.AddReconditioningTask(description, Money.Of(cost, currency)));
            created++;
        }

        if (created > 0)
            await context.SaveChangesAsync(cancellationToken);

        return Result.Success(new ReconditioningImportResult(created, errors));
    }

    // "2500.50" (invariant) or "2500,50" / "2.500,50" (es-AR), the two forms a spreadsheet here exports.
    private static bool TryParseCost(string text, out decimal cost) =>
        decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out cost)
        || decimal.TryParse(text, NumberStyles.Number, CultureInfo.GetCultureInfo("es-AR"), out cost);
}
