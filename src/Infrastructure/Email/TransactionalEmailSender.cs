using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using Application.Abstractions.Email;
using Application.Abstractions.Messaging;

namespace Infrastructure.TransactionalEmail;

/// <summary>
/// sfc A2.3 / A18: renders an embedded HTML template (simple <c>{{Token}}</c> replacement, no Razor) and sends it
/// through the existing <see cref="IEmailService"/> transport. Every value is HTML-encoded; the subject is the
/// static <c>&lt;title&gt;</c> of the template.
/// </summary>
internal sealed partial class TransactionalEmailSender : ITransactionalEmailSender
{
    private readonly IEmailService _transport;

    public TransactionalEmailSender(IEmailService transport)
    {
        _transport = transport;
    }

    [GeneratedRegex(@"\{\{(\w+)\}\}")]
    private static partial Regex TokenRegex();

    [GeneratedRegex(@"<title>(.*?)</title>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex TitleRegex();

    public async Task SendAsync(
        EmailTemplate template,
        string recipient,
        IReadOnlyDictionary<string, string> model,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recipient);
        ArgumentNullException.ThrowIfNull(model);

        IReadOnlyList<string> expected = TransactionalEmailTokens.For(template);
        string[] missing = expected.Where(t => !model.ContainsKey(t)).ToArray();
        string[] unknown = model.Keys.Where(k => !expected.Contains(k)).ToArray();
        if (missing.Length > 0 || unknown.Length > 0)
        {
            throw new ArgumentException(
                $"Template {template}: missing tokens [{string.Join(", ", missing)}], unknown tokens [{string.Join(", ", unknown)}].",
                nameof(model));
        }

        string html = LoadTemplate(template);
        string body = TokenRegex().Replace(html, m => WebUtility.HtmlEncode(model[m.Groups[1].Value]));

        await _transport.SendEmailAsync(recipient, ExtractSubject(html), body, cancellationToken);
    }

    internal static string LoadTemplate(EmailTemplate template)
    {
        string resource = $"Email.Templates.{template}.html";
        using Stream stream = typeof(TransactionalEmailSender).Assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException($"Email template resource '{resource}' not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    internal static string ExtractSubject(string html)
    {
        Match match = TitleRegex().Match(html);
        return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
    }
}
