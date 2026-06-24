using Application.Abstractions.Messaging;

namespace Application.DealerSettings.Get;

/// <summary>
/// Devuelve los settings del dealer del usuario autenticado.
/// El DealerId se resuelve via ICurrentTenantService (JWT claim).
/// </summary>
public sealed record GetDealerSettingsQuery() : IQuery<DealerSettingsResponse>;
