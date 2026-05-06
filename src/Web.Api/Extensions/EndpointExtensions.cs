using System.Reflection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Web.Api.Endpoints;

namespace Web.Api.Extensions;

public static class EndpointExtensions
{
    public static IServiceCollection AddEndpoints(this IServiceCollection services, Assembly assembly)
    {
        ServiceDescriptor[] serviceDescriptors = assembly
            .DefinedTypes
            .Where(type => type is { IsAbstract: false, IsInterface: false } &&
                           type.IsAssignableTo(typeof(IEndpoint)))
            .Select(type => ServiceDescriptor.Transient(typeof(IEndpoint), type))
            .ToArray();

        foreach (var descriptor in serviceDescriptors)
        {
            Console.WriteLine($"[DEBUG] Registering endpoint: {descriptor.ImplementationType?.Name}");
        }

        services.TryAddEnumerable(serviceDescriptors);

        return services;
    }

    public static IEndpointRouteBuilder MapEndpoints(
        this IEndpointRouteBuilder builder)
    {
        IEnumerable<IEndpoint> endpoints = builder.ServiceProvider.GetRequiredService<IEnumerable<IEndpoint>>();

        int count = 0;
        foreach (IEndpoint endpoint in endpoints)
        {
            endpoint.MapEndpoint(builder);
            count++;
        }

        Console.WriteLine($"[DEBUG] Mapped {count} endpoints");

        return builder;
    }

    public static RouteHandlerBuilder HasPermission(this RouteHandlerBuilder app, string permission)
    {
        return app.RequireAuthorization(permission);
    }
}
