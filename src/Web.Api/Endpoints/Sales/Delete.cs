// using Application.Sales.Delete;
// using MediatR;
// using SharedKernel;
// using Web.Api.Infrastructure;

// namespace Web.Api.Endpoints.Sales;

// internal sealed class Delete : IEndpoint
// {
//     public void MapEndpoint(IEndpointRouteBuilder app)
//     {
//         app.MapDelete("sales/{id}", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
//         {
//             var command = new DeleteSaleCommand(id);

//             Result result = await sender.Send(command, cancellationToken);

//             if (result.IsFailure)
//             {
//                 return Results.BadRequest(result.Error);
//             }

//             return Results.NoContent();
//         })
//         .WithTags("Sales");
//     }
// }
