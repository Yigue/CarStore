using Application.Dealers.Provision;

namespace Application.Common;

/// <summary>
/// Reserved subdomain blocklist enforced by <see cref="ProvisionDealerCommandValidator"/>
/// and the <see cref="Application.Dealers.CheckSubdomain.CheckSubdomainAvailabilityQueryHandler"/>.
/// Source of truth for system-critical slugs that cannot be self-provisioned.
/// Mirrored on the FE in <c>src/lib/validations/onboarding.ts</c> — both files must be updated together.
/// </summary>
public static class ReservedSubdomains
{
    public static readonly System.Collections.Generic.HashSet<string> Reserved =
        new(System.StringComparer.OrdinalIgnoreCase)
        {
            "admin",
            "api",
            "www",
            "app",
            "mail",
            "support",
            "dashboard",
            "static",
            "cdn",
            "auth",
            "help",
            "status",
            "billing",
            "root",
            "system",
            "internal",
        };
}