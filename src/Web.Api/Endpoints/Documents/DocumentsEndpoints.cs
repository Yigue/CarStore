using System.Threading;
using System.Threading.Tasks;
using Application.Documents.Commands.UploadDocument;
using Application.Documents.Commands.VerifyDocument;
using Application.Documents.Queries.GetClientDocuments;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Web.Api.Endpoints;

namespace Web.Api.Endpoints.Documents;

public class DocumentEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/documents")
            .WithTags("Documents")
            .RequireAuthorization();

        group.MapPost("/upload", UploadDocument)
            .WithName("UploadDocument")
            .DisableAntiforgery();

        group.MapPost("/{id:guid}/verify", VerifyDocument)
            .WithName("VerifyDocument");

        group.MapGet("/client/{clientId:guid}", GetClientDocuments)
            .WithName("GetClientDocuments");
    }

    private static async Task<IResult> UploadDocument(
        [FromBody] UploadDocumentRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var command = new UploadDocumentCommand(
            request.ClientId,
            request.Type,
            request.Base64Content,
            request.FileName,
            request.ContentType);

        var result = await sender.Send(command, cancellationToken);

        if (result.IsSuccess)
        {
            return Results.Ok(new { DocumentId = result.Value });
        }

        return Results.BadRequest(result.Error);
    }

    private static async Task<IResult> VerifyDocument(
        Guid id,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var command = new VerifyDocumentCommand(id);
        var result = await sender.Send(command, cancellationToken);

        return result.IsSuccess
            ? Results.Ok()
            : Results.BadRequest(result.Error);
    }

    private static async Task<IResult> GetClientDocuments(
        Guid clientId,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var query = new GetClientDocumentsQuery(clientId);
        var result = await sender.Send(query, cancellationToken);

        if (result.IsSuccess)
        {
            return Results.Ok(result.Value);
        }

        return Results.BadRequest(result.Error);
    }
}

public sealed record UploadDocumentRequest(
    Guid ClientId,
    Domain.Documents.DocumentType Type,
    string Base64Content,
    string FileName,
    string ContentType
);