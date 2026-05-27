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
}
