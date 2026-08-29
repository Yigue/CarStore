namespace Domain.Leads;

/// <summary>
/// What happened to a lead, as one closed vocabulary.
/// <para>
/// Deliberately an enum rather than free strings. Several event handlers write into the same
/// timeline from different aggregates, and with strings the codebase ends up with six spellings
/// of the same thing within months — at which point filtering or counting the history stops
/// working. Persisted by name (see <c>LeadActivityConfiguration</c>) so audit rows stay readable
/// straight from the database.
/// </para>
/// </summary>
public enum LeadActivityType
{
    Created = 0,
    StatusChanged = 1,
    AgentAssigned = 2,
    VehicleLinked = 3,
    NoteAdded = 4,
    QuoteCreated = 5,
    QuoteAccepted = 6,
    QuoteRejected = 7,
    AppointmentScheduled = 8,
    ClientCreated = 9,
    SaleRegistered = 10,
}
