// using Application.Financial.GetById;
// using MediatR;
// using SharedKernel;
// using Web.Api.Infrastructure;

// namespace Web.Api.Endpoints.Financial;

// internal sealed class GetById : IEndpoint
// {
//     public void MapEndpoint(IEndpointRouteBuilder app)
//     {
//         app.MapGet("financial/{id}", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
//         {
//             var query = new GetFinancialByIdQuery(id);

//             Result<FinancialResponse> result = await sender.Send(query, cancellationToken);

//             if (result.IsFailure)
//             {
//                 return Results.NotFound(result.Error);
//             }

//             return Results.Ok(result.Value);
//         })
//         .WithTags("Clients");
//     }
// }
