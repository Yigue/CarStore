using Application.Leads.RegisterWhatsAppClickIntent;
using MediatR;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Public;

/// <summary>
/// sfc C0.3 / B14: registers a "clicked WhatsApp to chat" intent before the visitor's browser opens
/// wa.me. Public and anonymous like <c>quotes/inquiry</c> — same rate-limiting policy.
/// </summary>
internal sealed class RegisterWhatsAppClickIntent : IEndpoint
{
    public sealed class Request
    {
        public Guid? CarId { get; set; }
        public string Code { get; set; } = string.Empty;
    }

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("public/whatsapp/click-intent", async (
            Request request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var command = new RegisterWhatsAppClickIntentCommand(request.CarId, request.Code);

            Result<Guid> result = await sender.Send(command, cancellationToken);

            return result.Match(
                id => Results.Created($"/leads/{id}", new { id }),
                CustomResults.Problem);
        })
        .AllowAnonymous()
        .RequireRateLimiting("public")
        .WithTags(Tags.Leads)
        .WithName("RegisterWhatsAppClickIntent")
        .Produces<Guid>(StatusCodes.Status201Created)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
