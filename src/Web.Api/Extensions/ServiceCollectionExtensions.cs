using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.OpenApi.Models;

namespace Web.Api.Extensions;

internal static class ServiceCollectionExtensions
{
    internal static IServiceCollection AddSwaggerGenWithAuth(this IServiceCollection services)
    {
        services.AddSwaggerGen(o =>
        {
            o.CustomSchemaIds(id => id.FullName!.Replace('+', '-'));
            
            // REMARK: AddSwaggerGen will be called once, but we need to configure docs per version.
            // This is usually done via IConfigureOptions<SwaggerGenOptions> in a separate class,
            // but for simplicity here we assume v1 for now and we will fix it if needed.
            // Let's use the standard approach: ConfigureOptions.
        });

        services.ConfigureOptions<ConfigureSwaggerOptions>();

        return services;
    }
}
