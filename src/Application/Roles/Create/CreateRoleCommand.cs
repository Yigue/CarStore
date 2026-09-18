using Application.Abstractions.Messaging;

namespace Application.Roles.Create;

/// <summary>
/// CFG-03: a dealership defines the roles its own structure needs — "Gerente de Taller",
/// "Vendedor Junior" — instead of squeezing everyone into Admin or Empleado.
/// </summary>
public sealed record CreateRoleCommand(
    string Name,
    string Description,
    IReadOnlyList<string> Permissions) : ICommand<Guid>;
