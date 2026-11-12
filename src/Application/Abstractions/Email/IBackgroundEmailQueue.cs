namespace Application.Abstractions.Email;

/// <summary>
/// Hands a transactional email to a background worker instead of sending it on the request path, for anonymous
/// endpoints whose response time must not reveal whether an email went out (sfc B5.1: newsletter enumeration).
/// Delivery is best effort: the queue lives in memory, and a restart drops what was not sent yet.
/// </summary>
public interface IBackgroundEmailQueue
{
    /// <summary>False when the queue is full; the email is dropped and the caller answers as usual.</summary>
    bool TryEnqueue(EmailTemplate template, string recipient, IReadOnlyDictionary<string, string> model);
}
