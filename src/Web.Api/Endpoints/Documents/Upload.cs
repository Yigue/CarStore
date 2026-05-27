using Application.Documents.Commands.UploadAndVerifyDocument;
using Application.Documents.DTOs;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Documents;

/// <summary>
/// PHASE-3: <c>POST /api/v1/documents/ocr-upload</c>
/// Multipart upload, optional <c>clientId</c> query param, returns a
/// <see cref="VerifyDocumentResultDto"/> with the OCR result and verification
/// outcome.
/// </summary>
internal sealed class Upload : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("documents/ocr-upload",
            async (
                IFormFile file,
                [FromQuery] Guid? clientId,
                ISender sender,
                CancellationToken ct) =>
            {
                if (file is null || file.Length == 0)
                {
                    return Results.BadRequest(new
                    {
                        code = "Document.EmptyFile",
                        description = "Se requiere un archivo no vacío.",
                    });
                }

                await using var stream = file.OpenReadStream();

                var command = new UploadAndVerifyDocumentCommand(
                    FileStream: stream,
                    ContentType: file.ContentType,
                    FileName: file.FileName,
                    ClientId: clientId);

                Result<VerifyDocumentResultDto> result = await sender.Send(command, ct);

                return result.Match(
                    dto => Results.Created($"/api/v1/documents/{dto.DocumentId}", dto),
                    CustomResults.Problem);
            })
        .DisableAntiforgery()
        .HasPermission(Permissions.DocumentsCreate)
        .WithTags(Tags.Documents)
        .WithName("UploadAndVerifyDocument")
        .Accepts<IFormFile>("multipart/form-data")
        .Produces<VerifyDocumentResultDto>(StatusCodes.Status201Created)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
