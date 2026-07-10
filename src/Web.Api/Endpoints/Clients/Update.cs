using Application.Clients.Update;
using Domain.Clients.Attributes;
using MediatR;
using SharedKernel;
using Web.Api.Infrastructure;
using System.Text.Json.Serialization;

namespace Web.Api.Endpoints.Clients;

internal sealed class Update : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("clients/{id}", async (Guid id, UpdateClientRequest request, ISender sender, CancellationToken cancellationToken) =>
        {
            var command = new UpdateClientCommand(
                id,
                request.FirstName,
                request.LastName,
                request.DNI,
                request.Email,
                request.Phone,
                request.Address,
                request.Status,
                request.Type,
                request.City,
                request.ZipCode,
                request.Notes);

            Result result = await sender.Send(command, cancellationToken);

            return result.Match(
                () => Results.NoContent(),
                CustomResults.Problem);
        })
        .HasPermission(Permissions.ClientsWrite)
        .WithTags(Tags.Clients)
        .WithName("UpdateClient")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }   
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UpdateClientRequest(
    string FirstName,
    string LastName,
    string DNI,
    string Email,
    string Phone,
    string Address,
    ClientStatus Status,
    Domain.Clients.Attributes.ClientType Type,
    string? City = null,
    string? ZipCode = null,
    string? Notes = null);
