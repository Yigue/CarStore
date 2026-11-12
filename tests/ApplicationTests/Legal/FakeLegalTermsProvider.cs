using Application.Legal;

namespace Application.UnitTests.Legal;

internal sealed class FakeLegalTermsProvider : ILegalTermsProvider
{
    public const string CurrentVersion = "2026-10-01";
    public static readonly string CurrentSha = new('b', 64);

    public LegalTermsInfo Current { get; set; } = new(CurrentVersion, CurrentSha, Material: true);

    public static LegalAcceptanceRecorder Recorder(Application.Abstractions.Data.IApplicationDbContext context, DateTime? now = null) =>
        new(context, new FakeLegalTermsProvider(), new FakeDateTimeProvider { UtcNow = now ?? new DateTime(2026, 10, 5, 0, 0, 0, DateTimeKind.Utc) });
}
