namespace Domain.Cars.Attributes;
public enum StatusServiceCar
{
    Service,
    EnVenta,
    Vendido,
    Disponible,
    NoDisponible,
    // Reservado: un vehículo Disponible tomado por una cotización activa (D-1).
    // Se libera (-> Disponible) al rechazar/expirar la cotización, o pasa a Vendido al concretar la venta.
    Reservado,

    // sfc B1 / ADR C3 — ciclo de vida del vehículo (RN-005). La columna se persiste como string
    // (HasConversion<string>), por lo que agregar miembros al final no requiere migración.
    // Ingreso: unidad recién ingresada, aún sin decisión comercial.
    Ingreso,
    // Reacondicionamiento: en preparación (equivalente moderno del legado Service).
    Reacondicionamiento,
    // Entregado: vendida y entregada al comprador (Sale.DeliveryDate confirmada).
    Entregado
}
