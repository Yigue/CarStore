using System.Threading;
using System.Threading.Tasks;
using Application.Documents.Commands.UploadDocument;
using Application.Documents.Commands.VerifyDocument;
using Application.Documents.Queries.GetClientDocuments;
using Application.Documents.Queries.GetSaleDocuments;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Web.Api.Endpoints;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Documents;

public class DocumentEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("documents")
            .WithTags("Documents")
            .RequireAuthorization();

        // D7 (qa-p1-integridad PR7, Slice 14): documents:create/documents:read shipped and
        // were verified live in PR6 (Slice 11.5) before this requirement was added —
        // otherwise this trades one 403 for another (finding 2).
        group.MapPost("/upload", UploadDocument)
            .WithName("UploadDocument")
            .DisableAntiforgery()
            .HasPermission(Permissions.DocumentsCreate);

        group.MapPost("/{id:guid}/verify", VerifyDocument)
            .WithName("VerifyDocument");

        group.MapGet("/client/{clientId:guid}", GetClientDocuments)
            .WithName("GetClientDocuments");

        group.MapGet("/sale/{saleId:guid}", GetSaleDocuments)
            .WithName("GetSaleDocuments");
    }

    private static async Task<IResult> UploadDocument(
        [FromBody] UploadDocumentRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var command = new UploadDocumentCommand(
            ClientId: request.ClientId,
            Type: request.Type,
            Base64Content: request.Base64Content,
            FileName: request.FileName,
            ContentType: request.ContentType,
            SaleId: request.SaleId);

        var result = await sender.Send(command, cancellationToken);

        // D7 Slice 13 (finding 10): Results.BadRequest(result.Error) squashed every
        // failure — including NotFound — to 400. result.Match(..., CustomResults.Problem)
        // lets a NotFound Result actually reach the wire as 404.
        return result.Match(
            id => Results.Ok(new { DocumentId = id }),
            CustomResults.Problem);
    }

    private static async Task<IResult> VerifyDocument(
        Guid id,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var command = new VerifyDocumentCommand(id);
        var result = await sender.Send(command, cancellationToken);

        return result.Match(
            () => Results.Ok(),
            CustomResults.Problem);
    }

    private static async Task<IResult> GetSaleDocuments(
        Guid saleId,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var query = new GetSaleDocumentsQuery(saleId);
        var result = await sender.Send(query, cancellationToken);

        return result.Match(
            Results.Ok,
            CustomResults.Problem);
    }

    private static async Task<IResult> GetClientDocuments(
        Guid clientId,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var query = new GetClientDocumentsQuery(clientId);
        var result = await sender.Send(query, cancellationToken);

        return result.Match(
            Results.Ok,
            CustomResults.Problem);
    }
}

public sealed record UploadDocumentRequest(
    Guid ClientId,
    Domain.Documents.DocumentType Type,
    string Base64Content,
    string FileName,
    string ContentType,
    Guid? SaleId = null
);