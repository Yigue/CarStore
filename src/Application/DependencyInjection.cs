using Application.Abstractions.Behaviors;
using Application.Billing.Entitlements;
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

        services.AddScoped<Application.Legal.LegalAcceptanceRecorder>();

        // sfc A3.1: needed regardless of whether Stripe/billing is enabled — entitlement limits are
        // also checked when running with NoOpPlanCatalog (fail-open: no configured limits → unlimited).
        services.AddScoped<IEntitlementService, EntitlementServiceHandler>();

        return services;
    }
}
