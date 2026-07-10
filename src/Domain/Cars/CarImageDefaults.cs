namespace Domain.Cars;

/// <summary>
/// Canonical placeholders and stable defaults for car image rendering.
/// Used by the defensive read-path (<see cref="Application.Cars.Search.SearchCarsQueryHandler"/>)
/// when a <c>car_images</c> row has no usable URL field — the resolver falls back to a
/// predictable public path so the FE can render a generic placeholder instead of a broken image.
/// </summary>
public static class CarImageDefaults
{
    /// <summary>
    /// Public path to a stable SVG placeholder served by the web tier. Returned by
    /// <c>GetPrimaryImageUrlAsync</c> when every URL field on the image row is null/empty.
    /// </summary>
    public const string NoImagePlaceholderUrl = "/images/placeholder-car.svg";
}
