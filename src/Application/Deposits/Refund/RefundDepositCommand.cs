using Application.Abstractions.Messaging;

namespace Application.Deposits.Refund;

/// <summary>sfc B1.3.5 / RN-003: records a manual refund the dealer already made outside the system.</summary>
public sealed record RefundDepositCommand(Guid DepositId, decimal Amount, string Reason) : ICommand;
