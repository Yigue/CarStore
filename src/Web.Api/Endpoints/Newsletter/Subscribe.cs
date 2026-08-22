using Application.Clients.Create;
using Domain.Clients.Attributes;
using MediatR;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Newsletter;

internal sealed class Subscribe : IEndpoint
{
    public sealed record Request(string Email);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("newsletter/subscribe", async (
            Request request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@'))
            {
                return Results.BadRequest(new { error = "Email no es válido." });
            }

            // qa-p1-integridad D6: named arguments — CreateClientCommand's positional order
            // (FirstName, LastName, DNI, Email, Phone, Address, Type, ...) previously landed the
            // subscriber's email in DNI and the literal "N/A" in Email, so new Email("N/A") always
            // threw. See tests/ArchitectureTests/CommandConstructionTests.cs for the ratchet.
            var command = new CreateClientCommand(
                FirstName: "Newsletter",
                LastName: "Suscriptor",
                DNI: $"NL-{Guid.NewGuid().ToString()[..8]}",
                Email: request.Email,
                Phone: "N/A",
                Address: "Suscripto via Web",
                Type: ClientType.Individual); // Newsletter subscribers default to Individual

            Result<Guid> result = await sender.Send(command, cancellationToken);

            return result.Match(
                id => Results.Ok(new { success = true, message = "Suscripto exitosamente." }),
                CustomResults.Problem);
        })
        .AllowAnonymous()
        .WithTags("Newsletter")
        .WithName("SubscribeNewsletter")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
