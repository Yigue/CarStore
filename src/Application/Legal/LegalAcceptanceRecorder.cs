using Application.Abstractions.Data;
using Domain.Legal;
using SharedKernel;

namespace Application.Legal;

/// <summary>
/// Validates that the client accepted the currently published Terms and Conditions (RN-016) and
/// stages the append-only <see cref="LegalAcceptance"/> row in the caller's unit of work.
/// </summary>
public sealed class LegalAcceptanceRecorder(
    IApplicationDbContext context,
    ILegalTermsProvider terms,
    IDateTimeProvider clock)
{
    public Result EnsureCurrentVersion(string? acceptedVersion) =>
        string.Equals(acceptedVersion?.Trim(), terms.Current.Version, StringComparison.Ordinal)
            ? Result.Success()
            : Result.Failure(LegalErrors.TermsVersionMismatch);

    public void RecordTermsAcceptance(Guid dealerId, Guid userId, string? ip, string? userAgent)
    {
        LegalTermsInfo current = terms.Current;
        context.LegalAcceptances.Add(LegalAcceptance.Create(
            dealerId,
            userId,
            LegalDocumentKind.Terms,
            current.Version,
            current.Sha256,
            clock.UtcNow,
            ip,
            userAgent));
    }
}
