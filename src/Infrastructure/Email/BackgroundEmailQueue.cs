using System.Threading.Channels;
using Application.Abstractions.Email;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.TransactionalEmail;

internal sealed record QueuedEmail(EmailTemplate Template, string Recipient, IReadOnlyDictionary<string, string> Model);

/// <summary>sfc B5.1: bounded in-memory queue; when full, new emails are dropped rather than slowing the request.</summary>
internal sealed class BackgroundEmailQueue : IBackgroundEmailQueue
{
    public const int Capacity = 1000;

    private readonly Channel<QueuedEmail> _channel = Channel.CreateBounded<QueuedEmail>(
        new BoundedChannelOptions(Capacity) { FullMode = BoundedChannelFullMode.Wait, SingleReader = true });

    public ChannelReader<QueuedEmail> Reader => _channel.Reader;

    public bool TryEnqueue(EmailTemplate template, string recipient, IReadOnlyDictionary<string, string> model) =>
        _channel.Writer.TryWrite(new QueuedEmail(template, recipient, model));
}

/// <summary>Sends the queued emails one at a time, each in its own DI scope; a failed send is logged and skipped.</summary>
internal sealed class BackgroundEmailWorker(
    BackgroundEmailQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<BackgroundEmailWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (QueuedEmail email in queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
                ITransactionalEmailSender sender = scope.ServiceProvider.GetRequiredService<ITransactionalEmailSender>();
                await sender.SendAsync(email.Template, email.Recipient, email.Model, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Could not send queued {Template} email", email.Template);
            }
        }
    }
}
