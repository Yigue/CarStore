using Application.Abstractions.Billing;
using Application.Billing.EventHandlers;
using Microsoft.Extensions.Configuration;

namespace InfrastructureTests.Billing;

/// <summary>
/// sfc A2.2 / A11 / RN-022 / H7: dunning emails go to the owner (+ <c>BillingEmail?</c>), in Spanish, on every failed
/// attempt and on <c>trial_will_end</c>.
/// </summary>
public class SendPaymentFailedEmailHandlerTests : IDisposable
{
    private static readonly DateTime T1 = new(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly BillingHandlerTestDb _t = new();
    private readonly SendPaymentFailedEmailHandler _handler;

    public SendPaymentFailedEmailHandlerTests() => _handler = HandlerWithBaseUrl("https://app.test/");

    private SendPaymentFailedEmailHandler HandlerWithBaseUrl(string baseUrl)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Frontend:BaseUrl"] = baseUrl })
            .Build();
        return new SendPaymentFailedEmailHandler(
            _t.Db, _t.Email, configuration, BillingHandlerTestLogger.For<SendPaymentFailedEmailHandler>());
    }

    public void Dispose() => _t.Dispose();

    private static PaymentFailed Failed(int attempt, string customer = BillingHandlerTestDb.CustomerId) =>
        new($"evt_f{attempt}", T1, customer, "sub_a22", attempt);

    [Fact]
    public async Task PaymentFailed_GoesToOwnerOnly_NotToOtherUsers_WhenNoBillingEmail()
    {
        await _t.SeedAsync();

        await _handler.Handle(Failed(1), CancellationToken.None);

        _t.Email.Sent.Should().ContainSingle().Which.To.Should().Be("owner@autosdelsur.test");
    }

    [Fact]
    public async Task PaymentFailed_GoesToDealerSettingsOwnerUserId_NotToTheOldestAdmin()
    {
        // sfc C17 / A4.3: after an owner transfer the owner is not the oldest Admin any more.
        await _t.SeedAsync(ownerMode: "newer");

        await _handler.Handle(Failed(1), CancellationToken.None);

        _t.Email.Sent.Should().ContainSingle().Which.To.Should().Be("newer-admin@autosdelsur.test");
    }

    [Fact]
    public async Task PaymentFailed_WithoutOwnerUserId_FallsBackToContactEmail_NotToAnAdminGuessedByRole()
    {
        await _t.SeedAsync(ownerMode: "none");

        await _handler.Handle(Failed(1), CancellationToken.None);

        _t.Email.Sent.Should().ContainSingle().Which.To.Should().Be("contacto@autosdelsur.test");
    }

    [Fact]
    public async Task PaymentFailed_GoesToOwnerAndBillingEmail()
    {
        await _t.SeedAsync(billingEmail: "facturacion@autosdelsur.test");

        await _handler.Handle(Failed(1), CancellationToken.None);

        _t.Email.Sent.Select(e => e.To).Should().BeEquivalentTo("owner@autosdelsur.test", "facturacion@autosdelsur.test");
    }

    [Fact]
    public async Task BillingEmailEqualToOwner_IsSentOnlyOnce()
    {
        await _t.SeedAsync(billingEmail: "OWNER@autosdelsur.test");

        await _handler.Handle(Failed(1), CancellationToken.None);

        _t.Email.Sent.Should().HaveCount(1);
    }

    [Fact]
    public async Task PaymentFailed_IsInSpanish_WithAttemptCountAndPortalLink()
    {
        await _t.SeedAsync();

        await _handler.Handle(Failed(2), CancellationToken.None);

        var mail = _t.Email.Sent.Single();
        mail.Subject.Should().Be("Tu pago no pudo procesarse");
        mail.Body.Should().Contain("Marta").And.Contain("intento 2").And.Contain("https://app.test/dashboard/configuracion");
        mail.Body.Should().NotContain("Dear").And.NotContain("failed");
    }

    [Fact]
    public async Task EveryAttempt_SendsAnEmail()
    {
        await _t.SeedAsync();

        await _handler.Handle(Failed(1), CancellationToken.None);
        await _handler.Handle(Failed(2), CancellationToken.None);

        _t.Email.Sent.Should().HaveCount(2);
    }

    [Fact]
    public async Task TrialWillEnd_SendsTrialNoticeInSpanish()
    {
        await _t.SeedAsync();

        await _handler.Handle(
            new TrialWillEnd("evt_t1", T1, BillingHandlerTestDb.CustomerId, "sub_a22", T1.AddDays(3)), CancellationToken.None);

        var mail = _t.Email.Sent.Single();
        mail.To.Should().Be("owner@autosdelsur.test");
        mail.Subject.Should().Contain("prueba");
        mail.Body.Should().Contain("Marta").And.Contain("04/03/2026");
    }

    [Fact]
    public async Task UnknownCustomer_SendsNothing()
    {
        await _t.SeedAsync();

        await _handler.Handle(Failed(1, "cus_unknown"), CancellationToken.None);

        _t.Email.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task EmailFailure_DoesNotThrow()
    {
        await _t.SeedAsync();
        _t.Email.ThrowOnSend = true;

        var act = () => _handler.Handle(Failed(1), CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task PaymentFailed_HtmlEncodesOwnerFirstName()
    {
        await _t.SeedAsync(ownerFirstName: "<img src=x onerror=alert(1)>");

        await _handler.Handle(Failed(1), CancellationToken.None);

        _t.Email.Sent.Single().Body.Should().Contain("&lt;img").And.NotContain("<img");
    }

    [Fact]
    public async Task TrialWillEnd_HtmlEncodesOwnerFirstName()
    {
        await _t.SeedAsync(ownerFirstName: "<script>x</script> & co");

        await _handler.Handle(
            new TrialWillEnd("evt_t2", T1, BillingHandlerTestDb.CustomerId, "sub_a22", T1.AddDays(3)), CancellationToken.None);

        _t.Email.Sent.Single().Body.Should().Contain("&lt;script&gt;x&lt;/script&gt; &amp; co").And.NotContain("<script>");
    }

    [Fact]
    public async Task PortalUrl_IsHtmlEncoded_InBothEmails()
    {
        await _t.SeedAsync();
        var handler = HandlerWithBaseUrl("https://app.test/?a=1&b=<x>");

        await handler.Handle(Failed(1), CancellationToken.None);
        await handler.Handle(
            new TrialWillEnd("evt_t3", T1, BillingHandlerTestDb.CustomerId, "sub_a22", T1.AddDays(3)), CancellationToken.None);

        foreach (var mail in _t.Email.Sent)
        {
            mail.Body.Should().Contain("https://app.test/?a=1&amp;b=&lt;x&gt;/dashboard/configuracion").And.NotContain("<x>");
        }
    }

    [Fact]
    public async Task NormalNameAndFixedSpanishText_AreUnchanged()
    {
        await _t.SeedAsync(ownerFirstName: "María");

        await _handler.Handle(Failed(2), CancellationToken.None);

        _t.Email.Sent.Single().Body.Should().Be(
            "Hola María,\n\n" +
            "No pudimos procesar el pago de la suscripción de tu concesionaria (intento 2).\n\n" +
            "Actualizá tu medio de pago para evitar la interrupción del servicio: https://app.test/dashboard/configuracion\n\n" +
            "Si ya lo actualizaste, ignorá este mensaje.\n\nEquipo CarStore");
    }
}
