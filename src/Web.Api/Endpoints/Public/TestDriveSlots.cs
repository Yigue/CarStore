using Application.Public.TestDrive;
using MediatR;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Public;

/// <summary>
/// sfc B5.2.4 / RN-044: public, anonymous — available test-drive slots for a vehicle on a given
/// day. Read-only, so no CAPTCHA/honeypot (those guard writes); same rate-limit policy as every
/// other public endpoint.
/// </summary>
internal sealed class TestDriveSlots : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("public/cars/{id:guid}/test-drive-slots", async (
            Guid id,
            DateOnly date,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var query = new TestDriveSlotsQuery(id, date);

            Result<IReadOnlyList<TestDriveSlotResponse>> result = await sender.Send(query, cancellationToken);

            return result.Match(
                Results.Ok,
                CustomResults.Problem);
        })
        .AllowAnonymous()
        .RequireRateLimiting("public")
        .WithTags(Tags.Cars)
        .WithName("GetTestDriveSlots")
        .Produces<IReadOnlyList<TestDriveSlotResponse>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status429TooManyRequests)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
