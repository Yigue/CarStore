using Application.DealerSettings;
using Application.DealerSettings.Commands.UpdateHostName;
using Domain.DealerSettings;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.DealerSettings;

/// <summary>
/// PUT /api/v1/dealer-settings/hostname
/// task 1.5.1: updates the tenant's public Slug and HostName.
/// Validates RFC 1035 at the domain layer (DomainException → 400 via GlobalExceptionHandler).
/// Returns 409 on UNIQUE constraint violation (hostname/slug already taken).
/// </summary>
internal sealed class UpdateHostName : IEndpoint
{
    public sealed record Request(string Slug, string HostName);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("dealer-settings/hostname", async (
            Request request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var command = new UpdateHostNameCommand(request.Slug, request.HostName);

            try
            {
                Result<DealerSettingsResponse> result = await sender.Send(command, cancellationToken);
                return result.Match(Results.Ok, CustomResults.Problem);
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
            {
                return CustomResults.Problem(
                    Result.Failure<DealerSettingsResponse>(
                        DealerSettingsErrors.HostNameConflict));
            }
        })
        .HasPermission("CanManageSettings")
        .WithTags(Tags.DealerSettings)
        .WithName("UpdateHostName")
        .Produces<DealerSettingsResponse>(StatusCodes.Status200OK)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }

    /// <summary>
    /// Detects UNIQUE constraint violations from both PostgreSQL and SQLite.
    /// PostgreSQL: inner exception is Npgsql.PostgresException with SqlState "23505".
    /// SQLite: inner exception message contains "UNIQUE constraint failed".
    /// </summary>
    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        var inner = ex.InnerException;
        if (inner is null) return false;

        // PostgreSQL
        if (inner.GetType().Name == "PostgresException")
        {
            // SqlState 23505 = unique_violation
            var sqlState = inner.GetType().GetProperty("SqlState")?.GetValue(inner) as string;
            return sqlState == "23505";
        }

        // SQLite (used in tests)
        return inner.Message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase);
    }
}
