using Application.Reports.GetPurchasesBySupplier;
using Application.Suppliers.Create;
using Application.Suppliers.Get;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Suppliers;

/// <summary>sfc B7.4 / RN-039: suppliers and purchases per supplier.</summary>
internal sealed class Suppliers : IEndpoint
{
    public sealed record Request(string Name, string? ContactEmail, string? Phone);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("suppliers", async (ISender sender, CancellationToken cancellationToken) =>
        {
            Result<IReadOnlyList<SupplierResponse>> result = await sender.Send(new GetSuppliersQuery(), cancellationToken);
            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission("cars:read")
        .WithTags(Tags.Cars)
        .WithName("GetSuppliers");

        app.MapPost("suppliers", async (Request request, ISender sender, CancellationToken cancellationToken) =>
        {
            Result<Guid> result = await sender.Send(
                new CreateSupplierCommand(Name: request.Name, ContactEmail: request.ContactEmail, Phone: request.Phone), cancellationToken);
            return result.Match(id => Results.Created($"/suppliers/{id}", new { id }), CustomResults.Problem);
        })
        .HasPermission("cars:create")
        .WithTags(Tags.Cars)
        .WithName("CreateSupplier");

        app.MapGet("reports/purchases-by-supplier", async (
            [FromQuery] DateTime from, [FromQuery] DateTime to, ISender sender, CancellationToken cancellationToken) =>
        {
            Result<IReadOnlyList<SupplierPurchasesResponse>> result =
                await sender.Send(new GetPurchasesBySupplierQuery(from, to), cancellationToken);
            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission("financial:read")
        .WithTags(Tags.Reports)
        .WithName("GetPurchasesBySupplier");
    }
}
