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

    public SendPaymentFailedEmailHandlerTests()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Frontend:BaseUrl"] = "https://app.test/" })
            .Build();
        _handler = new SendPaymentFailedEmailHandler(
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
}
