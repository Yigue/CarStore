using Application.Cars.Update;
using Domain.Cars;
using Domain.Cars.Atribbutes;
using MediatR;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Cars;

internal sealed class Update : IEndpoint
{
    public sealed class Request
    {
        public string Marca { get; set; }
        public string Modelo { get; set; }
        public int Color { get; set; }
        public int CarType { get; set; }
        public int CarStatus { get; set; }
        public int ServiceCar { get; set; }
        public int CantidadPuertas { get; set; }
        public int CantidadAsientos { get; set; }
        public int Cilindrada { get; set; }
        public int Kilometraje { get; set; }
        public int Año { get; set; }
        public string Patente { get; set; }
        public string Descripcion { get; set; }
        public decimal Precio { get; set; } 
    }

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("cars/{id}", async (Guid id, Request request, ISender sender, CancellationToken cancellationToken) =>
        {
            var command = new UpdateCarCommand(
                id,
                new Guid(request.Marca),
                new Guid(request.Modelo),
                (Color)request.Color,
                (TypeCar)request.CarType,
                (StatusCar)request.CarStatus,
                (statusServiceCar)request.ServiceCar,
                request.CantidadPuertas,
                request.CantidadAsientos,
                request.Cilindrada,
                request.Kilometraje,
                request.Año,
                request.Patente,
                request.Descripcion,
                request.Precio
            );

            Result<Guid> result = await sender.Send(command, cancellationToken);

            return result.Match(
                _ => Results.NoContent(),
                CustomResults.Problem);
        })
        .HasPermission(Permissions.CarsUpdate)
        .WithTags(Tags.Cars)
        .WithName("UpdateCar")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
