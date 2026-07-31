using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;

namespace Web.Api.Infrastructure;

internal sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is DomainException domainException)
        {
            logger.LogWarning(domainException, "Domain rule violation: {Message}", domainException.Message);

            var problemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Type = ProblemTypes.BadRequest,
                Title = "Domain Rule Violation",
                Detail = domainException.Message
            };

            httpContext.Response.StatusCode = problemDetails.Status.Value;
            await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
            return true;
        }

        // qa-p1-integridad D1: with RouteHandlerOptions.ThrowOnBadRequest converged to true
        // (Web.Api/DependencyInjection.cs), a framework binding failure now always throws instead
        // of writing its own response directly. Let it through with its own StatusCode intact
        // instead of rewriting it to 500 — that rewrite was the actual defect.
        if (exception is BadHttpRequestException badHttpRequestException)
        {
            logger.LogWarning(badHttpRequestException, "Bad HTTP request: {Message}", badHttpRequestException.Message);

            var badRequestProblemDetails = new ProblemDetails
            {
                Status = badHttpRequestException.StatusCode,
                Type = ProblemTypes.BadRequest,
                Title = "Bad Request",
                Detail = badHttpRequestException.Message
            };

            httpContext.Response.StatusCode = badRequestProblemDetails.Status.Value;
            await httpContext.Response.WriteAsJsonAsync(badRequestProblemDetails, cancellationToken);
            return true;
        }

        // A malformed JSON body fails during model-binding deserialization and surfaces here as a
        // raw JsonException (not wrapped in BadHttpRequestException) — map it to 400 explicitly.
        if (exception is JsonException jsonException)
        {
            logger.LogWarning(jsonException, "Malformed JSON request body: {Message}", jsonException.Message);

            var jsonProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Type = ProblemTypes.BadRequest,
                Title = "Malformed Request Body",
                Detail = "The request body could not be parsed as valid JSON."
            };

            httpContext.Response.StatusCode = jsonProblemDetails.Status.Value;
            await httpContext.Response.WriteAsJsonAsync(jsonProblemDetails, cancellationToken);
            return true;
        }

        // Deliberately NOT mapped here (api-error-contract spec, "FormatException Is Not Globally
        // Reclassified As A Client Error"): FormatException — and every exception type outside
        // BadHttpRequestException / JsonException / DomainException above — keeps today's opaque
        // 500. Malformed base64 in document upload is caught by a validator before the throwing
        // call (qa-p1-integridad PR7), not here; mapping FormatException globally would reclassify
        // genuine server bugs anywhere else in the app as client errors. This narrows what is
        // rewritten, it does not widen what counts as a client error.
        logger.LogError(exception, "Unhandled exception occurred");

        var serverProblemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Type = ProblemTypes.ServerError,
            Title = "Server failure",
            Detail = "An internal error occurred. Please contact support."
        };

        httpContext.Response.StatusCode = serverProblemDetails.Status.Value;

        await httpContext.Response.WriteAsJsonAsync(serverProblemDetails, cancellationToken);

        return true;
    }
}
