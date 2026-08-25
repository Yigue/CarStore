using Application.Abstractions.Messaging;

namespace Application.Users.ChangePassword;

/// <summary>
/// Cambio de contraseña de la sesión en curso.
/// </summary>
/// <remarks>
/// Deliberadamente NO lleva un UserId. El usuario objetivo sale de
/// <c>IUserContext.UserId</c> dentro del handler, así que la ruta no puede usarse
/// para cambiarle la contraseña a otro: no hay parámetro que manipular. Un
/// <c>userId</c> en el cuerpo o en la URL habría exigido una comprobación de
/// autorización extra en cada call site, y alcanza con que el dato no exista.
/// </remarks>
public sealed record ChangePasswordCommand(string CurrentPassword, string NewPassword) : ICommand;
