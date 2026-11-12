namespace Application.Public.TestDrive;

/// <summary>
/// sfc B5.2 / RN-044: shared constants between the public slot query and the booking command, so
/// both always agree on how long a test drive slot is.
/// </summary>
public static class TestDriveScheduling
{
    /// <summary>
    /// Fixed slot length for every public test-drive booking. Not configurable per dealer —
    /// out of scope for B5.2; the dealer only configures <em>when</em> it is open
    /// (<see cref="Domain.DealerSettings.DealerSettings.GetBusinessHours"/>), not how long each
    /// test drive lasts.
    /// </summary>
    public const int SlotDurationMinutes = 30;
}
