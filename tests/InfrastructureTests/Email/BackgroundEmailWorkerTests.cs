using Application.Abstractions.Email;
using FluentAssertions;
using Infrastructure.TransactionalEmail;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace InfrastructureTests.Email;

/// <summary>sfc B5.1: queued emails are sent off the request path; one failed send does not stop the next.</summary>
public class BackgroundEmailWorkerTests
{
    [Fact]
    public async Task Sends_every_queued_email_even_after_a_failure()
    {
        var sent = new List<string>();
        var allSent = new TaskCompletionSource();
        var sender = new Mock<ITransactionalEmailSender>();
        sender.Setup(s => s.SendAsync(It.IsAny<EmailTemplate>(), "boom@example.com", It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("smtp down"));
        sender.Setup(s => s.SendAsync(It.IsAny<EmailTemplate>(), "ok@example.com", It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .Callback(() => { sent.Add("ok@example.com"); allSent.TrySetResult(); })
            .Returns(Task.CompletedTask);

        ServiceProvider provider = new ServiceCollection().AddScoped(_ => sender.Object).BuildServiceProvider();
        var queue = new BackgroundEmailQueue();
        var worker = new BackgroundEmailWorker(queue, provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<BackgroundEmailWorker>.Instance);

        queue.TryEnqueue(EmailTemplate.NewsletterConfirm, "boom@example.com", new Dictionary<string, string>()).Should().BeTrue();
        queue.TryEnqueue(EmailTemplate.NewsletterConfirm, "ok@example.com", new Dictionary<string, string>()).Should().BeTrue();

        await worker.StartAsync(CancellationToken.None);
        await allSent.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await worker.StopAsync(CancellationToken.None);

        sent.Should().Equal("ok@example.com");
    }

    [Fact]
    public void A_full_queue_rejects_instead_of_blocking()
    {
        var queue = new BackgroundEmailQueue();
        for (int i = 0; i < BackgroundEmailQueue.Capacity; i++)
        {
            queue.TryEnqueue(EmailTemplate.NewsletterConfirm, $"{i}@example.com", new Dictionary<string, string>()).Should().BeTrue();
        }

        queue.TryEnqueue(EmailTemplate.NewsletterConfirm, "late@example.com", new Dictionary<string, string>()).Should().BeFalse();
    }
}
