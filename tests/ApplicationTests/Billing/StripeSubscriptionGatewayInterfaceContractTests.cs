using Application.Abstractions.Billing;
using Domain.Billing;
using FluentAssertions;
using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ApplicationTests.Billing;

public class StripeSubscriptionGatewayInterfaceContractTests
{
    [Fact]
    public void ISubscriptionGateway_ShouldHaveExactlyRequiredMethods()
    {
        var type = typeof(ISubscriptionGateway);
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance);

        methods.Should().HaveCount(4);

        // Method 1: CreateCheckoutSessionAsync
        var m1 = methods.SingleOrDefault(m => m.Name == "CreateCheckoutSessionAsync");
        m1.Should().NotBeNull();
        m1.ReturnType.Should().Be<Task<string>>();
        var p1 = m1.GetParameters();
        p1.Should().HaveCount(3);
        p1[0].ParameterType.Should().Be<Guid>(); // dealerId
        p1[1].ParameterType.Should().Be<string>(); // dealerEmail
        p1[2].ParameterType.Should().Be<CancellationToken>();

        // Method 2: GetStatusAsync
        var m2 = methods.SingleOrDefault(m => m.Name == "GetStatusAsync");
        m2.Should().NotBeNull();
        m2.ReturnType.Should().Be<Task<SubscriptionStatus>>();
        var p2 = m2.GetParameters();
        p2.Should().HaveCount(2);
        p2[0].ParameterType.Should().Be<string>(); // stripeSubscriptionId
        p2[1].ParameterType.Should().Be<CancellationToken>();

        // Method 3: CancelSubscriptionAsync
        var m3 = methods.SingleOrDefault(m => m.Name == "CancelSubscriptionAsync");
        m3.Should().NotBeNull();
        m3.ReturnType.Should().Be<Task>();
        var p3 = m3.GetParameters();
        p3.Should().HaveCount(2);
        p3[0].ParameterType.Should().Be<string>(); // stripeSubscriptionId
        p3[1].ParameterType.Should().Be<CancellationToken>();

        // Method 4: CreateCustomerAsync
        var m4 = methods.SingleOrDefault(m => m.Name == "CreateCustomerAsync");
        m4.Should().NotBeNull();
        m4.ReturnType.Should().Be<Task<string>>();
        var p4 = m4.GetParameters();
        p4.Should().HaveCount(3);
        p4[0].ParameterType.Should().Be<Guid>(); // dealerId
        p4[1].ParameterType.Should().Be<string>(); // email
        p4[2].ParameterType.Should().Be<CancellationToken>();
    }
}
