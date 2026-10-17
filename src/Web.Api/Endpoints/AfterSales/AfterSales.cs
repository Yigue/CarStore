using Application.AfterSales;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.AfterSales;

/// <summary>sfc C2.3/C2.4 / RN-049: warranties and service reminders.</summary>
internal sealed class AfterSales : IEndpoint
{
    public sealed record WarrantyRequest(int Months, string? Conditions);

    public sealed record ConsentRequest(bool Accepts);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("sales/{id:guid}/warranty", async (Guid id, WarrantyRequest request, ISender sender, CancellationToken cancellationToken) =>
        {
            Result<Guid> result = await sender.Send(new RegisterWarrantyCommand(id, request.Months, request.Conditions), cancellationToken);
            return result.Match(warrantyId => Results.Created($"/sales/{id}/warranty", new { id = warrantyId }), CustomResults.Problem);
        })
        .HasPermission("sales:update")
        .WithTags(Tags.Sales)
        .WithName("RegisterWarranty");

        app.MapGet("after-sales/reminders", async ([FromQuery] DateTime? dueUntil, ISender sender, CancellationToken cancellationToken) =>
        {
            Result<IReadOnlyList<ServiceReminderResponse>> result = await sender.Send(new GetServiceRemindersQuery(dueUntil), cancellationToken);
            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission("sales:read")
        .WithTags(Tags.Sales)
        .WithName("GetServiceReminders");

        app.MapPut("clients/{id:guid}/service-reminder-consent", async (Guid id, ConsentRequest request, ISender sender, CancellationToken cancellationToken) =>
        {
            Result result = await sender.Send(new SetServiceReminderConsentCommand(id, request.Accepts), cancellationToken);
            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission("clients:write")
        .WithTags(Tags.Clients)
        .WithName("SetServiceReminderConsent");
    }
}
