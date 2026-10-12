using Application.Abstractions.Messaging;

namespace Application.Sales.Void;

/// <summary>ADR C3 / RN-006. The actor is never part of the command: it comes from the tenant user context.</summary>
public sealed record VoidSaleCommand(Guid Id, string Reason) : ICommand;
