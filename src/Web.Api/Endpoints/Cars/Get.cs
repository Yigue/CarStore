
using Application.Cars.GetAll;
using MediatR;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Cars;

internal sealed class Get : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("cars", async (
            int? page,
            int? pageSize,
            string? sortBy,
            string? sortOrder,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var query = new GetAllCarsQuery(
                Page: page ?? 1,
                PageSize: pageSize ?? 20,
                SortBy: sortBy,
                SortOrder: sortOrder);

            Result<PaginatedResult<CarsResponses>> result = await sender.Send(query, cancellationToken);

            return result.Match(
                cars => Results.Ok(cars),
                CustomResults.Problem);
        })
        // Era AllowAnonymous, y su payload (CarsResponses) incluye `Patente` y
        // `PurchaseCost`. O sea: cualquiera sin token podía leer la patente de
        // todo el parque y el costo de compra de cada unidad — el margen de la
        // concesionaria. Verificado contra la base: un GET pelado devolvía
        // "AB123CD". `PurchaseCost` salía null sólo porque el seed no lo carga;
        // el campo estaba en la respuesta igual.
        //
        // No hace falta que sea público: el catálogo y el sitemap usan
        // `cars/search`, y los únicos consumidores de este endpoint son
        // pantallas del dashboard (useVehicles, VehiclesPage, ReportsPage,
        // ventas/nuevo), todas detrás de login.
        .HasPermission(Permissions.CarsRead)
        .WithTags(Tags.Cars)
        .WithName("GetAllCars")
        .Produces<PaginatedResult<CarsResponses>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
