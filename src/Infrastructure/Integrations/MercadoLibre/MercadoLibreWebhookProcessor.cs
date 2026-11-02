using Application.Abstractions.Data;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Deposits;
using Domain.Integrations;
using Domain.Leads;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharedKernel;

namespace Infrastructure.Integrations.MercadoLibre;

/// <summary>sfc C1.3 / RN-047: a MercadoLibre notification, bound to this codebase's types ahead of processing.</summary>
public sealed record MercadoLibreNotification(string Topic, string Resource, long UserId);

/// <summary>
/// sfc C1.3 / ADR C25, RN-047: inbound MercadoLibre notifications.
/// <para>
/// MercadoLibre's documented notification contract carries no HMAC-style signature (unlike Stripe or
/// Meta): authenticity comes from the notification referencing a resource we actually track for a
/// dealer with a <c>Connected</c> integration, followed by an authenticated pull of the authoritative
/// resource state from MercadoLibre's own API (<see cref="MercadoLibreClient.FetchItemAsync"/>) — the
/// webhook body itself is never trusted. This is MercadoLibre's own documented "pull to confirm"
/// practice for notification processing.
/// </para>
/// <para>
/// Scope note: resolving which dealer a notification belongs to is done here by matching the item id
/// against an <see cref="ExternalListing"/> row. MercadoLibre's "questions"/"orders" topics carry a
/// <c>resource</c> that is not literally an item path and a <c>user_id</c> that would need to be
/// matched against a stored MercadoLibre seller id on <c>DealerIntegration</c> to resolve the dealer
/// without first knowing the item — <c>DealerIntegration</c> does not persist that id, and adding it
/// is outside this PR's allowed Domain/Integrations surface (<see cref="ExternalListing"/> only). This
/// implementation treats <see cref="MercadoLibreNotification.Resource"/>'s trailing path segment as the
/// MercadoLibre item id for every supported topic, which is exact for MercadoLibre's own "items" topic
/// and is this PR's deliberate simplification for "questions"/"orders" pending that follow-up.
/// </para>
/// </summary>
public sealed class MercadoLibreWebhookProcessor(
    IApplicationDbContext dbContext,
    MercadoLibreClient client,
    Application.Abstractions.Integrations.IRateLimiter rateLimiter,
    IDateTimeProvider dateTimeProvider,
    ILogger<MercadoLibreWebhookProcessor> logger)
{
    private const string QuestionsTopic = "questions";
    private const string OrdersTopic = "orders";
    private const string OrdersV2Topic = "orders_v2";

    /// <summary>
    /// Security review (sfc C1.3), HIGH: MercadoLibre notifications carry no signature and
    /// <c>resource</c>/<c>topic</c> are fully attacker-controlled — anyone who knows a dealer's
    /// PUBLICLY LISTED item id (trivial; it is on the public marketplace listing) can POST
    /// unlimited forged notifications and spam that dealer's Lead CRM with fake "compra"/"pregunta"
    /// notes, since the pull-to-confirm step still re-fetches the item's real (public) data and
    /// proceeds. A full fix needs a stored MercadoLibre seller id on DealerIntegration to validate
    /// against the notification's own user_id — out of this PR's allowed Domain/Integrations
    /// surface (see the type doc comment's scope note). This throttles the volume cheaply, reusing
    /// the EXISTING per-(dealer, provider) rate limiter from sfc C1.1, under a provider key
    /// distinct from the outbound "mercadolibre" bucket so it never competes with legitimate
    /// listing-sync calls. Documented residual gap, not a full close.
    /// </summary>
    private const string WebhookRateLimitProvider = "mercadolibre-webhook";

    public async Task<Result> ProcessAsync(MercadoLibreNotification notification, CancellationToken cancellationToken)
    {
        string? itemId = ExtractItemId(notification.Resource);
        if (itemId is null)
        {
            return Result.Success();
        }

        ExternalListing? listing = await dbContext.ExternalListings
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(l => l.Provider == MercadoLibreClient.ProviderKey && l.ExternalId == itemId, cancellationToken);

        if (listing is null)
        {
            // Not an item this dealer has ever published through CarStore — nothing to reconcile.
            return Result.Success();
        }

        var rateLimit = await rateLimiter.TryAcquireAsync(listing.DealerId, WebhookRateLimitProvider, cancellationToken);
        if (!rateLimit.Allowed)
        {
            logger.LogWarning(
                "MercadoLibre webhook rate-limited for dealer {DealerId}, item {ItemId}; retry after {RetryAfter}.",
                listing.DealerId, itemId, rateLimit.RetryAfter);
            return Result.Success();
        }

        // Pull-to-confirm: never trust the webhook body, fetch the authoritative item state using the
        // resolved dealer's own OAuth token.
        Result<MercadoLibreItemSnapshot> pulled = await client.FetchItemAsync(listing.DealerId, itemId, cancellationToken);
        if (pulled.IsFailure)
        {
            logger.LogWarning(
                "MercadoLibre webhook pull-to-confirm failed for item {ItemId} (dealer {DealerId}): {Error}",
                itemId, listing.DealerId, pulled.Error.Description);
            return Result.Failure(pulled.Error);
        }

        return notification.Topic switch
        {
            QuestionsTopic => await HandleQuestionAsync(listing, cancellationToken),
            OrdersTopic or OrdersV2Topic => await HandlePurchaseIntentAsync(listing, cancellationToken),
            _ => Result.Success(),
        };
    }

    /// <summary>
    /// RN-047 / ADR C25: CarStore is the single source of truth (push-only). A marketplace purchase
    /// intent over a car that already has a local reservation or an active deposit is rejected
    /// outright — it must never create or touch a Sale/Reservation. Reuses the exact same guard
    /// <see cref="Application.Sales.Create.CreateSaleCommandHandler"/> applies for its own reservation
    /// and deposit checks.
    /// </summary>
    private async Task<Result> HandlePurchaseIntentAsync(ExternalListing listing, CancellationToken cancellationToken)
    {
        Car? car = await dbContext.Cars
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == listing.CarId, cancellationToken);

        if (car is null)
        {
            return Result.Success();
        }

        bool reservedOrSold = car.ServiceCar is StatusServiceCar.Reservado or StatusServiceCar.Vendido or StatusServiceCar.Entregado;

        bool hasActiveDeposit = await dbContext.Deposits
            .IgnoreQueryFilters()
            .AnyAsync(d => d.CarId == car.Id && d.Status == DepositStatus.Active, cancellationToken);

        if (reservedOrSold || hasActiveDeposit)
        {
            return Result.Failure(MercadoLibreWebhookErrors.CarNotAvailable(car.Id));
        }

        // Still push-only: a genuine buy intent from the marketplace becomes a high-priority Lead,
        // never a Sale — CarStore's own sale flow remains the only path that can create one.
        await CreateOrAppendLeadAsync(listing, car, isPurchaseIntent: true, cancellationToken);
        return Result.Success();
    }

    private async Task<Result> HandleQuestionAsync(ExternalListing listing, CancellationToken cancellationToken)
    {
        Car? car = await dbContext.Cars
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == listing.CarId, cancellationToken);

        if (car is null)
        {
            return Result.Success();
        }

        await CreateOrAppendLeadAsync(listing, car, isPurchaseIntent: false, cancellationToken);
        return Result.Success();
    }

    /// <summary>Mirrors <c>CreateInquiryCommandHandler</c>'s find-or-append shape: one open Lead per car/source, never duplicated.</summary>
    private async Task CreateOrAppendLeadAsync(ExternalListing listing, Car car, bool isPurchaseIntent, CancellationToken cancellationToken)
    {
        DateTime now = dateTimeProvider.UtcNow;
        string note = isPurchaseIntent
            ? $"[{now:yyyy-MM-dd HH:mm}] Intención de compra desde MercadoLibre (item {listing.ExternalId})."
            : $"[{now:yyyy-MM-dd HH:mm}] Pregunta recibida desde MercadoLibre (item {listing.ExternalId}).";

        Lead? existing = await dbContext.Leads
            .IgnoreQueryFilters()
            .Where(l => l.DealerId == listing.DealerId && l.InterestedVehicleId == car.Id && l.Source == LeadSource.MercadoLibre)
            .OrderByDescending(l => l.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is not null)
        {
            // Security review (sfc C1.3), second round: Notes has a 2000-char column cap
            // (LeadConfiguration). The first fix truncated the APPENDED value from the front, which
            // can evict a salesperson's own hand-written notes (or earlier webhook history) once a
            // forged/spammy notification stream fills the cap — that is worse than losing a webhook
            // note. Once the field is already at/near the cap, stop appending further webhook noise
            // instead and keep whatever is already there untouched.
            const int maxNotesLength = 2000;
            string existingNotes = existing.Notes ?? string.Empty;
            if (existingNotes.Length + 1 + note.Length <= maxNotesLength)
            {
                existing.UpdateNotes(string.IsNullOrWhiteSpace(existingNotes) ? note : $"{existingNotes}\n{note}");
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            else
            {
                // Security review (sfc C1.3), third round: silently dropping this note means a
                // GENUINE purchase-intent notification arriving after the field fills up vanishes
                // with no trace — worth a warning even though the root cause (overloading a
                // human-authored field with machine-generated events instead of a dedicated,
                // deduplicated LeadActivity-style log, keyed by validated seller id) stays a
                // documented, deferred architectural gap: see tasks.md C1.3.2's residual-risk note.
                logger.LogWarning(
                    "MercadoLibre note dropped for Lead {LeadId} (dealer {DealerId}, item {ItemId}): Notes field is at its 2000-char cap.",
                    existing.Id, listing.DealerId, listing.ExternalId);
            }

            return;
        }

        // MercadoLibre's questions/orders APIs never expose the buyer's email (privacy); a stable
        // pseudo-email keyed on the item id is what keeps the find-or-append lookup above working.
        string pseudoEmail = $"ml-item-{listing.ExternalId.ToLowerInvariant()}@mercadolibre.marketplace";

        var lead = Lead.Create(
            listing.DealerId,
            $"Comprador MercadoLibre ({listing.ExternalId})",
            pseudoEmail,
            phone: string.Empty,
            LeadSource.MercadoLibre,
            now,
            car.Id);

        LeadAttribution attribution = LeadAttribution.Create(
            utmSource: "mercadolibre",
            utmMedium: isPurchaseIntent ? "marketplace_order" : "marketplace_question",
            utmCampaign: null,
            utmTerm: null,
            utmContent: null,
            referrer: "mercadolibre.com",
            landingPath: null,
            gclid: null,
            fbclid: null,
            firstTouchAtUtc: now);
        lead.SetFirstTouch(attribution);
        lead.UpdateNotes(note);

        dbContext.Leads.Add(lead);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>The trailing path segment of MercadoLibre's <c>resource</c> field — see the scope note on the type.</summary>
    private static string? ExtractItemId(string resource)
    {
        if (string.IsNullOrWhiteSpace(resource))
        {
            return null;
        }

        string trimmed = resource.Trim().TrimEnd('/');
        int lastSlash = trimmed.LastIndexOf('/');
        string segment = lastSlash >= 0 ? trimmed[(lastSlash + 1)..] : trimmed;
        return string.IsNullOrWhiteSpace(segment) ? null : segment;
    }
}

internal static class MercadoLibreWebhookErrors
{
    public static Error CarNotAvailable(Guid carId) => Error.Conflict(
        "MercadoLibreWebhook.CarNotAvailable",
        $"The car '{carId}' already has a local reservation or an active deposit; the marketplace purchase intent was rejected.");
}
