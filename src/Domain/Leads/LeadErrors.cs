using SharedKernel;

namespace Domain.Leads;

public static class LeadErrors
{
    public static Error NotFound(Guid leadId) => Error.NotFound(
        "Leads.NotFound",
        $"The lead with the Id = '{leadId}' was not found.");

    public static readonly Error CannotRegress = Error.Problem(
        "Leads.CannotRegress",
        "Un lead ganado no puede retroceder de etapa.");

    public static readonly Error NoAgentsAvailable = Error.Problem(
        "Leads.NoAgentsAvailable",
        "No hay agentes activos para asignar.");

    public static readonly Error InvalidAgent = Error.Problem(
        "Leads.InvalidAgent",
        "El agente asignado no es válido.");

    /// <summary>
    /// Ganado means the deal closed, and a closed deal has a sale behind it. Without this the
    /// stage was reachable with nothing to show for it, and "won" leads accumulated that no
    /// report could reconcile against revenue.
    /// </summary>
    public static readonly Error WonRequiresSale = Error.Problem(
        "Leads.WonRequiresSale",
        "Para marcar el lead como Ganado primero registrá la venta.");

    /// <summary>
    /// A stage names something that happened. Demostración without a booked appointment is a lead
    /// filed under an event nobody scheduled, and the agent who opens it next has no idea whether
    /// the demo exists.
    /// </summary>
    public static readonly Error DemoRequiresAppointment = Error.Problem(
        "Leads.DemoRequiresAppointment",
        "Para pasar el lead a Demostración primero agendá la cita.");

    /// <summary>
    /// Negotiating means there is a number on the table. Without a quote the stage records an
    /// intention, not a fact, and the pipeline stops meaning anything.
    /// </summary>
    public static readonly Error NegotiationRequiresQuote = Error.Problem(
        "Leads.NegotiationRequiresQuote",
        "Para pasar el lead a Negociación primero generá la cotización.");

    /// <summary>
    /// Contactado means a person owns this lead from here on. The gate used to be "notes were
    /// typed", which recorded that someone wrote something without ever saying who is
    /// responsible for the follow-up — leaving the lead ownerless in the one stage that implies
    /// an owner.
    /// </summary>
    public static readonly Error RequiresAssignedAgent = Error.Problem(
        "Leads.RequiresAssignedAgent",
        "Para pasar el lead a Contactado primero asigná un agente responsable.");
}
