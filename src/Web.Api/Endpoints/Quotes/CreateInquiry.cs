using Application.Quotes.CreateInquiry;
using MediatR;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Quotes;

internal sealed class CreateInquiry : IEndpoint
{
    public sealed class Request
    {
        public Guid? CarId { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Comments { get; set; } = string.Empty;
        // sfc C0.1 / RN-045: first-touch attribution captured by the public site; optional.
        public InquiryAttribution? Attribution { get; set; }
    }

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("quotes/inquiry", async (
            Request request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var command = new CreateInquiryCommand(
                CarId: request.CarId,
                FirstName: request.FirstName,
                LastName: request.LastName,
                Email: request.Email,
                Phone: request.Phone,
                Comments: request.Comments,
                Attribution: request.Attribution);

            Result<Guid> result = await sender.Send(command, cancellationToken);

            return result.Match(
                id => Results.Created($"/quotes/{id}", new { id }),
                CustomResults.Problem);
        })
        .AllowAnonymous()
        .RequireRateLimiting("public")
        .WithTags(Tags.Quotes)
        .WithName("CreatePublicInquiry")
        .Produces<Guid>(StatusCodes.Status201Created)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
