using System.Text;
using Application.Reconditioning.Csv;
using MediatR;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Cars;

/// <summary>sfc B7.3 / RN-038: reconditioning tasks of a car as CSV, in and out.</summary>
internal sealed class ReconditioningCsv : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("cars/{id:guid}/reconditioning/export.csv", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
        {
            Result<string> result = await sender.Send(new GetReconditioningCsvQuery(id), cancellationToken);
            return result.Match(
                csv => Results.File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray(),
                    "text/csv; charset=utf-8", $"reacondicionamiento-{id}.csv"),
                CustomResults.Problem);
        })
        .HasPermission("cars:read")
        .WithTags(Tags.Cars)
        .WithName("ExportReconditioningCsv");

        app.MapPost("cars/{id:guid}/reconditioning/import", async (Guid id, IFormFile file, ISender sender, CancellationToken cancellationToken) =>
        {
            // Checked before reading: the 2 MB limit must not be paid for by loading a bigger file into memory.
            if (file.Length > ImportReconditioningCsvCommand.MaxBytes)
                return CustomResults.Problem(Result.Failure(Error.Validation("CSV_TOO_LARGE", "El archivo supera el máximo de 2 MB.")));

            using var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8);
            string content = await reader.ReadToEndAsync(cancellationToken);
            Result<ReconditioningImportResult> result = await sender.Send(new ImportReconditioningCsvCommand(id, content), cancellationToken);
            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .DisableAntiforgery()
        .Accepts<IFormFile>("multipart/form-data")
        .HasPermission("cars:update")
        .WithTags(Tags.Cars)
        .WithName("ImportReconditioningCsv");
    }
}
