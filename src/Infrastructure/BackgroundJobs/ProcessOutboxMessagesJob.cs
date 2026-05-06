using System.Data;
using Application.Abstractions.Data;
using Domain.Shared;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Quartz;

using SharedKernel;

namespace Infrastructure.BackgroundJobs;

[DisallowConcurrentExecution]
public class ProcessOutboxMessagesJob(
    IApplicationDbContext dbContext,
    IPublisher publisher,
    ILogger<ProcessOutboxMessagesJob> logger) : IJob
{
    /// <summary>
    /// Explicit type map for safe deserialization of domain events.
    /// Only types in this dictionary can be instantiated from outbox messages.
    /// This eliminates the RCE vector from TypeNameHandling.All.
    /// </summary>
    private static readonly Dictionary<string, Type> DomainEventTypeMap =
        typeof(IDomainEvent).Assembly
            .GetTypes()
            .Where(t => typeof(IDomainEvent).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface)
            .ToDictionary(t => t.Name, t => t);

    private static readonly JsonSerializerSettings SafeSerializerSettings = new()
    {
        TypeNameHandling = TypeNameHandling.None
    };

    public async Task Execute(IJobExecutionContext context)
    {
        List<OutboxMessage> messages = await dbContext
            .OutboxMessages
            .Where(m => m.ProcessedOnUtc == null)
            .Take(20)
            .ToListAsync(context.CancellationToken);

        if (messages.Count == 0)
        {
            return;
        }

        foreach (OutboxMessage outboxMessage in messages)
        {
            if (!DomainEventTypeMap.TryGetValue(outboxMessage.Type, out Type? eventType))
            {
                logger.LogWarning(
                    "Unknown domain event type '{EventType}' in outbox message {MessageId}. Skipping.",
                    outboxMessage.Type,
                    outboxMessage.Id);

                outboxMessage.ProcessedOnUtc = DateTime.UtcNow;
                outboxMessage.Error = $"Unknown event type: {outboxMessage.Type}";
                continue;
            }

            IDomainEvent? domainEvent = (IDomainEvent?)JsonConvert
                .DeserializeObject(
                    outboxMessage.Content,
                    eventType,
                    SafeSerializerSettings);

            if (domainEvent is null)
            {
                logger.LogWarning(
                    "Failed to deserialize outbox message {MessageId} as {EventType}. Skipping.",
                    outboxMessage.Id,
                    outboxMessage.Type);

                outboxMessage.ProcessedOnUtc = DateTime.UtcNow;
                outboxMessage.Error = $"Deserialization returned null for type: {outboxMessage.Type}";
                continue;
            }

            try
            {
                await publisher.Publish(domainEvent, context.CancellationToken);

                outboxMessage.ProcessedOnUtc = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                outboxMessage.Error = ex.Message;
            }
        }

        await dbContext.SaveChangesAsync(context.CancellationToken);
    }
}
