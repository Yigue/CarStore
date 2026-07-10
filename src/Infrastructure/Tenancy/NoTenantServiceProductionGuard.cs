using Application.Abstractions.Tenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Tenancy;

/// <summary>
/// Startup filter that validates NoTenantService is never the active ICurrentTenantService
/// implementation in a Production environment.
///
/// Runs after all DI registrations (including test overrides via ConfigureTestServices)
/// so it catches accidental wiring of NoTenantService as the default.
/// </summary>
internal sealed class NoTenantServiceProductionGuard(IServiceProvider provider) : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        using var scope = provider.CreateScope();
        var tenantService = scope.ServiceProvider.GetService<ICurrentTenantService>();
        if (tenantService is NoTenantService)
        {
            throw new InvalidOperationException(
                "NoTenantService must not be the production default. " +
                "Register CurrentTenantService as ICurrentTenantService for production deployments.");
        }

        return next;
    }
}
