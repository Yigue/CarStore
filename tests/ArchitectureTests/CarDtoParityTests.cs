using System;
using System.Linq;
using System.Reflection;
using Application.Cars.GetAll;
using Application.Cars.GetById;
using FluentAssertions;
using Xunit;

namespace ArchitectureTests;

/// <summary>
/// A detail endpoint that exposes LESS than its own list endpoint is a silent defect: the
/// frontend adapter reads the missing field, gets <c>undefined</c>, and renders the "N/A"
/// fallback on a vehicle whose data is perfectly correct. Nothing fails, nothing logs — the
/// page just lies.
///
/// That is exactly how <c>CarGetByIdResponse</c> shipped without <c>FuelType</c> and
/// <c>Transmission</c> while <c>CarsResponses</c> carried both: reviewing either record in
/// isolation reads fine. Only the comparison exposes it.
///
/// This is the mechanism, not the convention. Every property on the list DTO must also exist
/// on the detail DTO with a compatible type. The detail DTO may expose MORE (it legitimately
/// does — images, purchase cost), never fewer.
/// </summary>
public class CarDtoParityTests
{
    /// <summary>
    /// The detail DTO narrows two properties on purpose, and both narrowings are authorization
    /// decisions rather than contract gaps — see the projection comments in
    /// <c>GetCarByIdQueryHandler</c>. Patente is nulled for anonymous callers because it
    /// identifies a physical vehicle; PurchaseCost is nulled for non-admins because it is the
    /// dealership's margin. Their presence is still enforced; only the nullability differs.
    /// </summary>
    private static readonly string[] NullableByAuthorization = ["Patente", "PurchaseCost"];

    [Fact]
    public void CarDetailResponse_Should_ExposeEveryPropertyTheListResponseExposes()
    {
        PropertyInfo[] listProperties = typeof(CarsResponses).GetProperties();
        PropertyInfo[] detailProperties = typeof(CarGetByIdResponse).GetProperties();

        string[] missing = listProperties
            .Select(p => p.Name)
            .Except(detailProperties.Select(p => p.Name), StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        missing.Should().BeEmpty(
            "the vehicle detail page renders these fields and silently falls back to 'N/A' when " +
            "the detail endpoint omits them; add them to CarGetByIdResponse and project them in " +
            "GetCarByIdQueryHandler");
    }

    [Fact]
    public void CarDetailResponse_Should_UseTheSameTypeAsTheListResponse_ForSharedProperties()
    {
        var detailProperties = typeof(CarGetByIdResponse)
            .GetProperties()
            .ToDictionary(p => p.Name, StringComparer.Ordinal);

        foreach (PropertyInfo listProperty in typeof(CarsResponses).GetProperties())
        {
            if (!detailProperties.TryGetValue(listProperty.Name, out PropertyInfo? detailProperty))
            {
                continue; // Reported by the parity test above; not this test's concern.
            }

            if (NullableByAuthorization.Contains(listProperty.Name, StringComparer.Ordinal))
            {
                continue;
            }

            Type listType = Nullable.GetUnderlyingType(listProperty.PropertyType) ?? listProperty.PropertyType;
            Type detailType = Nullable.GetUnderlyingType(detailProperty.PropertyType) ?? detailProperty.PropertyType;

            detailType.Should().Be(
                listType,
                "CarGetByIdResponse.{0} and CarsResponses.{0} feed the same frontend adapter, so a " +
                "type divergence deserializes into the wrong shape",
                listProperty.Name);
        }
    }
}
