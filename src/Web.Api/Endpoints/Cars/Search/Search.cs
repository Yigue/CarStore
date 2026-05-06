using Application.Cars.Search;
using MediatR;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Cars.Search;

internal sealed class Search : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("cars/search", async (
            [AsParameters] SearchCarsRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var query = new SearchCarsQuery
            {
                SearchTerm = request.SearchTerm,
                MarcaId = request.MarcaId,
                ModeloId = request.ModeloId,
                YearFrom = request.YearFrom,
                YearTo = request.YearTo,
                PriceFrom = request.PriceFrom,
                PriceTo = request.PriceTo,
                ColorIds = request.ColorIds?.ToList(),
                CarTypeIds = request.CarTypeIds?.ToList(),
                DoorsFrom = request.DoorsFrom,
                DoorsTo = request.DoorsTo,
                SortBy = request.SortBy,
                SortDescending = request.SortDescending ?? false,
                Page = request.Page ?? 1,
                PageSize = request.PageSize ?? 10
            };

            Result<SearchCarsResult> result = await sender.Send(query, cancellationToken);

            return result.Match(
                data => Results.Ok(data),
                CustomResults.Problem);
        })
        .AllowAnonymous()
        .WithTags(Tags.Cars)
        .WithName("SearchCars")
        .Produces<SearchCarsResult>(StatusCodes.Status200OK)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}

public sealed record SearchCarsRequest(
    string? SearchTerm,
    Guid? MarcaId,
    Guid? ModeloId,
    int? YearFrom,
    int? YearTo,
    decimal? PriceFrom,
    decimal? PriceTo,
    int[]? ColorIds,
    int[]? CarTypeIds,
    int? DoorsFrom,
    int? DoorsTo,
    string? SortBy,
    bool? SortDescending,
    int? Page,
    int? PageSize);
