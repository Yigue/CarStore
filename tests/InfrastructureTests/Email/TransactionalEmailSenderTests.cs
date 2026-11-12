using System.Reflection;
using System.Text.RegularExpressions;
using Application.Abstractions.Email;
using Application.Abstractions.Messaging;
using FluentAssertions;
using Infrastructure.TransactionalEmail;
using Moq;

namespace InfrastructureTests.Email;

public sealed partial class TransactionalEmailSenderTests
{
    [GeneratedRegex(@"\{\{(\w+)\}\}")]
    private static partial Regex TokenRegex();

    public static TheoryData<EmailTemplate> AllTemplates()
    {
        var data = new TheoryData<EmailTemplate>();
        foreach (EmailTemplate t in Enum.GetValues<EmailTemplate>())
        {
            data.Add(t);
        }
        return data;
    }

    private static Dictionary<string, string> ModelFor(EmailTemplate template, string value = "valor") =>
        TransactionalEmailTokens.For(template).ToDictionary(t => t, _ => value);

    private static (TransactionalEmailSender Sender, Mock<IEmailService> Transport) Build()
    {
        var transport = new Mock<IEmailService>();
        return (new TransactionalEmailSender(transport.Object), transport);
    }

    [Theory]
    [MemberData(nameof(AllTemplates))]
    public void EveryTemplate_ResolvesAnExistingEmbeddedHtml(EmailTemplate template)
    {
        string html = TransactionalEmailSender.LoadTemplate(template);

        html.Should().Contain("<html").And.Contain("<title>");
    }

    [Fact]
    public void NoOrphanTemplates_EveryEmbeddedHtmlHasAnEnumMember()
    {
        string[] resources = typeof(TransactionalEmailSender).Assembly.GetManifestResourceNames()
            .Where(n => n.Contains("Email.Templates.", StringComparison.Ordinal) && n.EndsWith(".html", StringComparison.Ordinal))
            .ToArray();

        resources.Should().HaveCount(Enum.GetValues<EmailTemplate>().Length);
    }

    [Theory]
    [MemberData(nameof(AllTemplates))]
    public void EveryTemplate_DeclaresExactlyTheTokensItUses(EmailTemplate template)
    {
        string html = TransactionalEmailSender.LoadTemplate(template);

        var used = TokenRegex().Matches(html).Select(m => m.Groups[1].Value).Distinct().Order().ToArray();

        used.Should().Equal(TransactionalEmailTokens.For(template).Order());
    }

    [Theory]
    [MemberData(nameof(AllTemplates))]
    public void EveryTemplate_HasAStaticSpanishSubject(EmailTemplate template)
    {
        string subject = TransactionalEmailSender.ExtractSubject(TransactionalEmailSender.LoadTemplate(template));

        subject.Should().NotBeNullOrWhiteSpace();
        subject.Should().NotContain("{{");
    }

    [Theory]
    [MemberData(nameof(AllTemplates))]
    public async Task Send_EncodesEveryInterpolatedValue(EmailTemplate template)
    {
        var (sender, transport) = Build();
        string body = "";
        transport.Setup(t => t.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, CancellationToken>((_, _, b, _) => body = b)
            .Returns(Task.CompletedTask);

        await sender.SendAsync(template, "owner@dealer.com", ModelFor(template, "<script>alert(1)</script>"));

        body.Should().NotContain("<script>");
        body.Should().Contain("&lt;script&gt;");
        body.Should().NotContain("{{");
    }

    [Fact]
    public async Task Send_UsesTheTransportWithRecipientAndTemplateSubject()
    {
        var (sender, transport) = Build();

        await sender.SendAsync(EmailTemplate.Welcome, "owner@dealer.com", ModelFor(EmailTemplate.Welcome, "Ana"));

        transport.Verify(t => t.SendEmailAsync(
            "owner@dealer.com",
            It.Is<string>(s => !string.IsNullOrWhiteSpace(s)),
            It.Is<string>(b => b.Contains("Ana")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Send_MissingToken_ThrowsAndSendsNothing()
    {
        var (sender, transport) = Build();
        Dictionary<string, string> model = ModelFor(EmailTemplate.PaymentFailed);
        model.Remove(model.Keys.First());

        Func<Task> act = () => sender.SendAsync(EmailTemplate.PaymentFailed, "a@b.com", model);

        await act.Should().ThrowAsync<ArgumentException>();
        transport.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Send_UnknownToken_ThrowsAndSendsNothing()
    {
        var (sender, transport) = Build();
        Dictionary<string, string> model = ModelFor(EmailTemplate.PaymentFailed);
        model["Bogus"] = "x";

        Func<Task> act = () => sender.SendAsync(EmailTemplate.PaymentFailed, "a@b.com", model);

        await act.Should().ThrowAsync<ArgumentException>();
        transport.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Send_BlankRecipient_Throws()
    {
        var (sender, _) = Build();

        Func<Task> act = () => sender.SendAsync(EmailTemplate.Welcome, " ", ModelFor(EmailTemplate.Welcome));

        await act.Should().ThrowAsync<ArgumentException>();
    }
}
