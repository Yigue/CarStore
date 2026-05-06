using Application.Cars.Create;
using Domain.Cars;
using Domain.Cars.Attributes;
using MediatR;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Cars;

internal sealed class Create : IEndpoint
{
    public sealed class Request
    {
        public Guid Marca { get; set; }
        public Guid Modelo { get; set; }
        public int Color { get; set; }
        public int CarType { get; set; }
        public int CarStatus { get; set; }
        public int ServiceCar { get; set; }
        public int CantidadPuertas { get; set; }
        public int CantidadAsientos { get; set; }
        public int Cilindrada { get; set; }
        public int Kilometraje { get; set; }
        public int Anio { get; set; }
        public string Patente { get; set; }
        public string Descripcion { get; set; }
        public decimal Precio { get; set; }
        }

        public void MapEndpoint(IEndpointRouteBuilder app)
        {
        app.MapPost("cars", async (Request request, ISender sender, CancellationToken cancellationToken) =>
        {
            var command = new CreateCarCommand(
                request.Marca,
                request.Modelo,
                (Color)request.Color,
                (TypeCar)request.CarType,
                (StatusCar)request.CarStatus,
                (StatusServiceCar)request.ServiceCar,
                request.CantidadPuertas,
                request.CantidadAsientos,
                request.Cilindrada,
                request.Kilometraje,
                request.Anio,
                request.Patente,
                request.Descripcion,
                request.Precio);

            Result<Guid> result = await sender.Send(command, cancellationToken);

            return result.Match(
                id => Results.Created($"/cars/{id}", new { id }),
                CustomResults.Problem);
        })
        .HasPermission(Permissions.CarsCreate)
        .WithTags(Tags.Cars)
        .WithName("CreateCar")
        .Produces<Guid>(StatusCodes.Status201Created)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
