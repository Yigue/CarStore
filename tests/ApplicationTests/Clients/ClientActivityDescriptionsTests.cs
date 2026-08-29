using Application.Clients.GetActivity;
using FluentAssertions;

namespace Application.UnitTests.Clients;

public class ClientActivityDescriptionsTests
{
    [Theory]
    [InlineData("SaleCompletedDomainEvent", "Venta completada")]
    [InlineData("QuoteRejectedDomainEvent", "Cotización rechazada")]
    [InlineData("ClientCreatedDomainEvent", "Cliente creado")]
    public void For_Should_ReturnAReadableSentence(string eventType, string expected)
    {
        ClientActivityDescriptions.For(eventType).Should().Be(expected);
    }

    /// <summary>
    /// An event added on the server without a sentence here must still read as something. Falling
    /// back to a trimmed class name is plain but honest; returning empty would make the row vanish
    /// from a timeline whose whole job is completeness.
    /// </summary>
    [Fact]
    public void For_Should_FallBackToTheTrimmedEventName()
    {
        ClientActivityDescriptions.For("SomethingNewDomainEvent").Should().Be("SomethingNew");
    }
}
