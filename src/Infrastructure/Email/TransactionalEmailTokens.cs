using Application.Abstractions.Email;

namespace Infrastructure.TransactionalEmail;

/// <summary>
/// sfc A2.3: the tokens each template accepts. The switch has no default arm, so adding an
/// <see cref="EmailTemplate"/> member without declaring its tokens raises compiler warning CS8509 at build time;
/// tests assert the declared tokens match the ones used in the HTML.
/// </summary>
internal static class TransactionalEmailTokens
{
    public static IReadOnlyList<string> For(EmailTemplate template) => template switch
    {
        EmailTemplate.Welcome => ["Name", "DealerName", "LoginUrl"],
        EmailTemplate.VerifyEmail => ["Name", "VerifyUrl", "ExpiresInHours"],
        EmailTemplate.Invitation => ["InviterName", "DealerName", "AcceptUrl", "ExpiresInDays"],
        EmailTemplate.OwnerTransfer => ["Name", "DealerName", "ConfirmUrl", "ExpiresInHours"],
        EmailTemplate.TrialWillEnd => ["Name", "EndDate", "PortalUrl"],
        EmailTemplate.PaymentFailed => ["Name", "Attempt", "PortalUrl"],
        EmailTemplate.Refunded => ["Name", "Amount", "Currency"],
        EmailTemplate.ExportReady => ["Name", "DownloadUrl", "ExpiresAt"],
        EmailTemplate.CancellationGrace => ["Name", "GraceEndDate", "ReactivateUrl"],
        EmailTemplate.PurgeWarning => ["Name", "PurgeDate", "ExportUrl"],
        EmailTemplate.QuoteSent => ["Name", "DealerName", "QuoteNumber", "QuoteUrl"],
        EmailTemplate.DepositExpiring => ["Name", "VehicleName", "DealerName", "ExpiresAt"],
    };
}
