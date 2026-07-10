using Application.Abstractions.Billing;
using Application.Abstractions.Data;
using Application.Billing.Commands.HandleStripeWebhook;
using Domain.Billing;
using Domain.Shared;
using Infrastructure.Billing;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Infrastructure.BackgroundJobs;

[DisallowConcurrentExecution]
public class ProcessStripeWebhooksJob : IJob
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IDealerSubscriptionRepository _subscriptionRepository;
    private readonly ProcessedStripeEventRepository _processedEventRepository;
    private readonly ISender _sender;
    private readonly ILogger<ProcessStripeWebhooksJob> _logger;

    public ProcessStripeWebhooksJob(
        IApplicationDbContext dbContext,
        IDealerSubscriptionRepository subscriptionRepository,
        ProcessedStripeEventRepository processedEventRepository,
        ISender sender,
        ILogger<ProcessStripeWebhooksJob> logger)
    {
        _dbContext = dbContext;
        _subscriptionRepository = subscriptionRepository;
        _processedEventRepository = processedEventRepository;
        _sender = sender;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var messages = await _dbContext.OutboxMessages
            .Where(m => m.Type == "StripeRaw" && m.ProcessedOnUtc == null)
            .OrderBy(m => m.OccurredOnUtc)
            .Take(20)
            .ToListAsync(context.CancellationToken);

        if (messages.Count == 0)
        {
            return;
        }

        foreach (var message in messages)
        {
            try
            {
                using var doc = JsonDocument.Parse(message.Content);
                var root = doc.RootElement;

                var eventId = root.GetProperty("id").GetString() ?? string.Empty;
                var eventType = root.GetProperty("type").GetString() ?? string.Empty;

                // Extract dealer ID and customer ID from the raw JSON
                Guid? dealerId = null;
                string? customerId = null;

                if (root.TryGetProperty("data", out var dataElement) &&
                    dataElement.TryGetProperty("object", out var dataObject))
                {
                    if (dataObject.TryGetProperty("customer", out var customerProp))
                    {
                        customerId = customerProp.GetString();
                    }

                    if (dataObject.TryGetProperty("metadata", out var metadataProp) &&
                        metadataProp.TryGetProperty("dealer_id", out var dealerIdProp))
                    {
                        var dealerIdStr = dealerIdProp.GetString();
                        if (Guid.TryParse(dealerIdStr, out var dId))
                        {
                            dealerId = dId;
                        }
                    }
                }

                if (dealerId == null && !string.IsNullOrEmpty(customerId))
                {
                    var existingSub = await _subscriptionRepository.GetByStripeCustomerIdAsync(customerId, context.CancellationToken);
                    if (existingSub != null)
                    {
                        dealerId = existingSub.DealerId;
                    }
                }

                // Check idempotency guard
                var isNewEvent = await _processedEventRepository.TryAddAsync(eventId, dealerId, context.CancellationToken);
                if (isNewEvent)
                {
                    var command = new HandleStripeWebhookCommand(eventId, eventType, message.Content);
                    var result = await _sender.Send(command, context.CancellationToken);

                    if (result.IsFailure)
                    {
                        throw new Exception($"Command failed: {result.Error.Description}");
                    }

                    _logger.LogInformation("Processed Stripe event {EventId} of type {EventType}", eventId, eventType);
                }
                else
                {
                    _logger.LogInformation("Duplicate Stripe event {EventId} ignored.", eventId);
                }

                message.ProcessedOnUtc = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process outbox Stripe webhook message {MessageId}", message.Id);
                message.ProcessedOnUtc = DateTime.UtcNow;
                message.Error = ex.ToString();
            }

            // Save after each event so subsequent events can query the persisted state
            await _dbContext.SaveChangesAsync(context.CancellationToken);
        }
    }
}
