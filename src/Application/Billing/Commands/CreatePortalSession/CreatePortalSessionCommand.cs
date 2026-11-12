using Application.Abstractions.Messaging;
using System;

namespace Application.Billing.Commands.CreatePortalSession;

public sealed record CreatePortalSessionCommand(Guid DealerId) : ICommand<PortalSessionResponse>;

public sealed record PortalSessionResponse(string Url);
