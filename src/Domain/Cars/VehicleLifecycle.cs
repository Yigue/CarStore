using Domain.Cars.Attributes;

namespace Domain.Cars;

/// <summary>
/// sfc B1 / ADR C3 — pure vehicle lifecycle state machine (RN-005, RN-006).
/// <code>
/// Ingreso → Reacondicionamiento → Disponible → Reservado → Vendido → Entregado
/// </code>
/// Allowed edges: Ingreso→{Reacondicionamiento, Disponible}, Reacondicionamiento→Disponible,
/// Disponible→{Reservado, Vendido}, Reservado→{Disponible, Vendido}, Vendido→Entregado.
/// Vendido→Disponible is NOT an edge here: it only happens through <c>Car.RevertSale</c>
/// (triggered by <c>Sale.Void</c>), never through a plain status change.
/// </summary>
public static class VehicleLifecycle
{
    private static readonly IReadOnlyDictionary<StatusServiceCar, StatusServiceCar[]> Edges =
        new Dictionary<StatusServiceCar, StatusServiceCar[]>
        {
            [StatusServiceCar.Ingreso] = [StatusServiceCar.Reacondicionamiento, StatusServiceCar.Disponible],
            [StatusServiceCar.Reacondicionamiento] = [StatusServiceCar.Disponible],
            [StatusServiceCar.Disponible] = [StatusServiceCar.Reservado, StatusServiceCar.Vendido],
            [StatusServiceCar.Reservado] = [StatusServiceCar.Disponible, StatusServiceCar.Vendido],
            [StatusServiceCar.Vendido] = [StatusServiceCar.Entregado],
            [StatusServiceCar.Entregado] = [],
        };

    /// <summary>Maps legacy persisted values onto lifecycle stages.</summary>
    public static StatusServiceCar Normalize(StatusServiceCar status) => status switch
    {
        StatusServiceCar.EnVenta => StatusServiceCar.Disponible,
        StatusServiceCar.Service => StatusServiceCar.Reacondicionamiento,
        StatusServiceCar.NoDisponible => StatusServiceCar.Ingreso,
        _ => status,
    };

    public static bool CanTransition(StatusServiceCar from, StatusServiceCar to) =>
        Edges.TryGetValue(Normalize(from), out StatusServiceCar[]? targets)
        && targets.Contains(Normalize(to));
}
