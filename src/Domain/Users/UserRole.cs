namespace Domain.Users;

/// <summary>
/// Roles del usuario dentro de una concesionaria.
/// Persistido como string para mantener legibilidad en la DB y permitir
/// agregar valores sin romper datos existentes.
/// </summary>
public enum UserRole
{
    Admin,
    Empleado,
    Cliente,
    Invitado,
    SuperAdmin
}
