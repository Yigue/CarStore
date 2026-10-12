namespace Application.Abstractions.Email;

/// <summary>
/// sfc A2.3 / A18: closed catalog of transactional emails. Every value maps to an HTML template in
/// <c>Infrastructure/Email/Templates</c> named after the enum member.
/// </summary>
public enum EmailTemplate
{
    Welcome,
    VerifyEmail,
    Invitation,
    OwnerTransfer,
    TrialWillEnd,
    PaymentFailed,
    Refunded,
    ExportReady,
    CancellationGrace,
    PurgeWarning,
    QuoteSent,
    DepositExpiring,
}
