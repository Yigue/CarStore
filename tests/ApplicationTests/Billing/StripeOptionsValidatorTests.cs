using FluentAssertions;
using Infrastructure.Billing;
using Xunit;

namespace ApplicationTests.Billing;

/// <summary>spr PR 1.1 (SPR-02/BE-BILLING-07, ADR B1, D1): "plan único" — exactly one sellable plan.</summary>
public class StripeOptionsValidatorTests
{
    private readonly StripeOptionsValidator _validator = new();

    [Fact]
    public void MoreThanOneSellablePlan_FailsValidation()
    {
        var options = new StripeOptions
        {
            SecretKey = "sk_test",
            WebhookSecret = "whsec_test",
            PriceId = "price_legacy",
            Plans =
            {
                ["standard"] = new StripePlanOptions { PriceId = "price_standard" },
                ["pro"] = new StripePlanOptions { PriceId = "price_pro" }
            }
        };

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("exactly one");
    }

    [Fact]
    public void ExactlyOneSellablePlan_ViaPlansDictionary_Succeeds()
    {
        var options = new StripeOptions
        {
            SecretKey = "sk_test",
            WebhookSecret = "whsec_test",
            PriceId = "price_legacy",
            Plans =
            {
                ["standard"] = new StripePlanOptions { PriceId = "price_standard" }
            }
        };

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void ExactlyOneSellablePlan_SynthesizedFromLegacyPriceId_Succeeds()
    {
        var options = new StripeOptions
        {
            SecretKey = "sk_test",
            WebhookSecret = "whsec_test",
            PriceId = "price_legacy"
        };

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void NoSellablePlan_FailsValidation()
    {
        var options = new StripeOptions
        {
            SecretKey = "sk_test",
            WebhookSecret = "whsec_test",
            PriceId = string.Empty
        };

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue();
    }
}
