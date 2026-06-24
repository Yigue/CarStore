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
    Reservado
}