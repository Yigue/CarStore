namespace Application.Abstractions.Email;

/// <summary>
/// sfc A2.3 / A18: sends a catalog email. The model maps template tokens to plain-text values; the sender
/// HTML-encodes every value, so callers must pass raw (unencoded) text.
/// </summary>
public interface ITransactionalEmailSender
{
    Task SendAsync(
        EmailTemplate template,
        string recipient,
        IReadOnlyDictionary<string, string> model,
        CancellationToken cancellationToken = default);
}
