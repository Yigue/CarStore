using SharedKernel;

namespace Domain.Billing;

public static class DealerSubscriptionErrors
{
    public static readonly Error InvalidTransition = Error.Problem(
        "DealerSubscription.InvalidTransition",
        "The requested subscription status transition is invalid.");

    public static readonly Error DuplicateForDealer = Error.Conflict(
        "DealerSubscription.DuplicateForDealer",
        "A subscription already exists for this dealer.");

    public static readonly Error TerminalCancelled = Error.Problem(
        "DealerSubscription.TerminalCancelled",
        "The subscription is cancelled and cannot be modified.");

    /// <summary>spr PR 1.1 (ADR B1): provisioning rejects a plan code that is not in <c>IPlanCatalog</c>.</summary>
    public static readonly Error UnknownPlanCode = Error.Validation(
        "DealerSubscription.UnknownPlanCode",
        "The requested plan code is not sellable.");
}
