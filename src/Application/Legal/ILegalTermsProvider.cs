namespace Application.Legal;

/// <summary>Currently published Terms and Conditions (RN-016).</summary>
/// <param name="Version">Identifier in <c>YYYY-MM-DD</c> format.</param>
/// <param name="Sha256">Hex SHA-256 of the published content.</param>
/// <param name="Material">True when the change requires blocking re-acceptance.</param>
public sealed record LegalTermsInfo(string Version, string Sha256, bool Material);

public interface ILegalTermsProvider
{
    LegalTermsInfo Current { get; }
}
