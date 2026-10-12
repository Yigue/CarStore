using System.Reflection;
using Domain.Cars;
using FluentAssertions;
using Xunit;

namespace ArchitectureTests;

/// <summary>
/// sfc B1.1 / RN-006: <c>Car.MarkAsAvailable</c> was a public back door from Vendido to
/// Disponible. Only <c>Car.RevertSale</c> (driven by Sale.Void) may leave Vendido.
/// </summary>
public class CarMarkAsAvailableVisibilityTests : BaseTest
{
    [Fact]
    public void MarkAsAvailable_Should_NotBePublic_SoApplicationCannotCallIt()
    {
        MethodInfo? method = typeof(Car).GetMethod(
            "MarkAsAvailable", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        method.Should().NotBeNull();
        method!.IsPublic.Should().BeFalse("only the sale void flow may revert a sold car");
    }
}
