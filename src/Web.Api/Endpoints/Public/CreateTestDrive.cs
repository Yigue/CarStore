using Application.Public.TestDrive;
using MediatR;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Public;

/// <summary>
/// sfc B5.2.2/B5.2.4 / RN-044: public, anonymous — books a test drive request (no agent
/// assigned yet, state <c>Requested</c>; a dashboard user confirms it later via
/// <c>POST appointments/{id}/confirm</c>, sfc B5.2.3). Guarded like every other anonymous write
/// in this product: CAPTCHA (<see cref="CaptchaEndpointFilter"/>), the shared "public" rate-limit
/// policy, and a honeypot field the handler checks (<see cref="Request.Website"/>).
/// </summary>
internal sealed class CreateTestDrive : IEndpoint
{
    public sealed class Request : IHasCaptchaToken
    {
        public Guid CarId { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public DateTime Start { get; set; }
        public string? Comments { get; set; }

        /// <summary>
        /// Honeypot (B5.2.2): rendered hidden via CSS on the public form — a real visitor never
        /// sees or fills this field. Non-empty means a bot.
        /// </summary>
        public string? Website { get; set; }

        public string? CaptchaToken { get; set; }
    }

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("public/test-drives", async (
            Request request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var command = new CreateTestDriveCommand(
                CarId: request.CarId,
                FirstName: request.FirstName,
                LastName: request.LastName,
                Email: request.Email,
                Phone: request.Phone,
                Start: request.Start,
                Comments: request.Comments,
                Website: request.Website);

            Result<Guid> result = await sender.Send(command, cancellationToken);

            return result.Match(
                id => Results.Created($"/appointments/{id}", new { id }),
                failure => failure.Error.Code == TestDriveErrors.TooManyRequested.Code
                    // RN-044's "third simultaneous Requested booking" throttle is a business rule,
                    // not the per-IP rate-limiting middleware policy — see TestDriveErrors'
                    // TooManyRequested doc comment for why it is special-cased here instead of
                    // widening CustomResults' shared ErrorType→status switch for one caller.
                    ? Results.Problem(
                        title: failure.Error.Code,
                        detail: failure.Error.Description,
                        type: "https://datatracker.ietf.org/doc/html/rfc6585#section-4",
                        statusCode: StatusCodes.Status429TooManyRequests)
                    : CustomResults.Problem(failure));
        })
        .AllowAnonymous()
        .RequireRateLimiting("public")
        // Runs after the rate limiter and BEFORE the handler — same convention as Provision.
        .AddEndpointFilter<CaptchaEndpointFilter>()
        .WithTags(Tags.Appointments)
        .WithName("CreateTestDrive")
        .Produces<Guid>(StatusCodes.Status201Created)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status429TooManyRequests)
        .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
