using Application.Abstractions.Integrations;
using Microsoft.AspNetCore.DataProtection;

namespace Infrastructure.Integrations;

/// <summary>sfc C1.1 / ADR C22: Data Protection with the versioned purpose; keys live in Postgres (EF key store).</summary>
public sealed class DataProtectionCredentialProtector(IDataProtectionProvider provider) : ICredentialProtector
{
    private readonly IDataProtector _protector = provider.CreateProtector(ICredentialProtector.Purpose);

    public string Protect(string plaintext) => _protector.Protect(plaintext);

    public string Unprotect(string protectedValue) => _protector.Unprotect(protectedValue);
}
