using System.Security.Cryptography;
using System.Text;
using Application.Abstractions.Data;
using Domain.Webhooks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;
using SharedKernel;

namespace Infrastructure.BackgroundJobs;

/// <summary>
/// Polls due <see cref="WebhookDelivery"/> rows (Pending, NextRetryAtUtc &lt;= now) and POSTs
/// the stored payload to the subscription URL. Retry state is fully persisted on the delivery
/// row (<see cref="WebhookDelivery.RecordFailure"/>/<see cref="WebhookRetryPolicy"/>), so this
/// job is a stateless poller — safe to run on multiple instances (each row is claimed via a
/// normal SaveChanges, no distributed lock needed at this volume).
/// </summary>
[DisallowConcurrentExecution]
public sealed class DispatchWebhookDeliveriesJob(
    IApplicationDbContext dbContext,
    IHttpClientFactory httpClientFactory,
    IDateTimeProvider dateTimeProvider,
    ILogger<DispatchWebhookDeliveriesJob> logger) : IJob
{
    public const string HttpClientName = "WebhookDelivery";

    private const string EventHeaderName = "X-CarStore-Event";
    private const string SignatureHeaderName = "X-CarStore-Signature";

    public async Task Execute(IJobExecutionContext context)
    {
        DateTime now = dateTimeProvider.UtcNow;

        List<WebhookDelivery> dueDeliveries = await dbContext.WebhookDeliveries
            .IgnoreQueryFilters() // background job runs without tenant context
            .Where(d => d.Status == WebhookDeliveryStatus.Pending && d.NextRetryAtUtc <= now)
            .OrderBy(d => d.NextRetryAtUtc)
            .Take(20)
            .ToListAsync(context.CancellationToken);

        if (dueDeliveries.Count == 0)
        {
            return;
        }

        List<Guid> subscriptionIds = dueDeliveries.Select(d => d.SubscriptionId).Distinct().ToList();
        Dictionary<Guid, WebhookSubscription> subscriptionsById = (await dbContext.WebhookSubscriptions
                .IgnoreQueryFilters()
                .Where(s => subscriptionIds.Contains(s.Id))
                .ToListAsync(context.CancellationToken))
            .ToDictionary(s => s.Id);

        HttpClient httpClient = httpClientFactory.CreateClient(HttpClientName);

        foreach (WebhookDelivery delivery in dueDeliveries)
        {
            if (!subscriptionsById.TryGetValue(delivery.SubscriptionId, out WebhookSubscription? subscription))
            {
                // Subscription was deleted after the delivery was enqueued — dead-letter it,
                // there is nowhere left to send it.
                delivery.RecordFailure(dateTimeProvider.UtcNow, statusCode: null, "Subscription no longer exists");
                continue;
            }

            if (!subscription.IsActive)
            {
                delivery.RecordFailure(dateTimeProvider.UtcNow, statusCode: null, "Subscription is inactive");
                continue;
            }

            await SendAsync(httpClient, subscription, delivery, context.CancellationToken);
        }

        await dbContext.SaveChangesAsync(context.CancellationToken);
    }

    private async Task SendAsync(
        HttpClient httpClient,
        WebhookSubscription subscription,
        WebhookDelivery delivery,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, subscription.Url);
            request.Content = new StringContent(delivery.Payload, Encoding.UTF8, "application/json");
            request.Headers.TryAddWithoutValidation(EventHeaderName, delivery.EventType);
            request.Headers.TryAddWithoutValidation(SignatureHeaderName, ComputeSignature(delivery.Payload, subscription.Secret));

            using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                delivery.RecordSuccess(dateTimeProvider.UtcNow, (int)response.StatusCode);
            }
            else
            {
                delivery.RecordFailure(
                    dateTimeProvider.UtcNow,
                    (int)response.StatusCode,
                    $"Non-success status code: {(int)response.StatusCode}");
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            logger.LogWarning(
                ex,
                "Webhook delivery {DeliveryId} to subscription {SubscriptionId} failed (attempt {Attempt})",
                delivery.Id,
                subscription.Id,
                delivery.AttemptCount + 1);

            delivery.RecordFailure(dateTimeProvider.UtcNow, statusCode: null, ex.Message);
        }
    }

    /// <summary>Hex-encoded HMAC-SHA256 of the raw request body, keyed by the subscription secret.</summary>
    /// <remarks>internal (not private) so InfrastructureTests can assert signature correctness directly.</remarks>
    internal static string ComputeSignature(string payload, string secret)
    {
        byte[] keyBytes = Encoding.UTF8.GetBytes(secret);
        byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);
        byte[] hash = HMACSHA256.HashData(keyBytes, payloadBytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
