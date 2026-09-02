using Application.Abstractions.Behaviors;
using Application.Leads.Activity;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(config =>
        {
            config.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly);

            config.AddOpenBehavior(typeof(RequestLoggingPipelineBehavior<,>));
            config.AddOpenBehavior(typeof(ValidationPipelineBehavior<,>));
        });

        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true);

        // Shared write path for the lead timeline, injected into the notification handlers that
        // populate it. Scoped so it joins the same unit of work as the handler that uses it.
        services.AddScoped<LeadActivityRecorder>();
        services.AddScoped<Application.Platform.AuditLogs.PlatformAuditLogRecorder>();

        return services;
    }
}
