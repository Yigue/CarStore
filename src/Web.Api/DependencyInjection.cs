using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Web.Api.Infrastructure;
using Asp.Versioning;

namespace Web.Api;

public static class DependencyInjection
{
    public static IServiceCollection AddPresentation(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();

        // REMARK: If you want to use Controllers, you'll need this.
        services.AddControllers();

        services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new ApiVersion(1);
            options.ApiVersionReader = new UrlSegmentApiVersionReader();
        }).AddApiExplorer(options =>
        {
            options.GroupNameFormat = "'v'V";
            options.SubstituteApiVersionInUrl = true;
        });

        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails();

        // qa-p1-integridad D1: RouteHandlerOptions.ThrowOnBadRequest was never configured, so it
        // silently defaulted to true only in Development and false everywhere else — one
        // environment threw into GlobalExceptionHandler (rewritten to a bare 500), the other wrote
        // its own bodiless 400 that bypassed the handler entirely. Forcing it true converges both
        // environments onto the same throw-then-handle path, so GlobalExceptionHandler's new
        // BadHttpRequestException/JsonException arms produce one consistent ProblemDetails 400.
        services.Configure<RouteHandlerOptions>(o => o.ThrowOnBadRequest = true);

        return services;
    }

    public static IServiceCollection AddFeatureFlags(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<FeatureFlagsOptions>(
            configuration.GetSection(FeatureFlagsOptions.SectionName));

        return services;
    }
}
