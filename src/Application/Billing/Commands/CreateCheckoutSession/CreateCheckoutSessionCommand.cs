using Application.Abstractions.Messaging;
using System;

namespace Application.Billing.Commands.CreateCheckoutSession;

public sealed record CreateCheckoutSessionCommand(Guid DealerId, string Email) : ICommand<CheckoutSessionResponse>;

public sealed record CheckoutSessionResponse(string Url);
