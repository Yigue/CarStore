using Application.Cars.Create;
using Domain.Cars;
using Domain.Cars.Atribbutes;
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
        public int Año { get; set; }
        public string Patente { get; set; }
        public string Descripcion { get; set; }
        public decimal Precio { get; set; }
    }

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("cars", async (Request request, ISender sender, CancellationToken cancellationToken) =>
        {
            var command = new CreateCarCommand
            {
                Marca = request.Marca,
                Modelo = request.Modelo,
                Color = (Color)request.Color,
                CarType = (TypeCar)request.CarType,
                CarStatus = (StatusCar)request.CarStatus,
                ServiceCar = (statusServiceCar)request.ServiceCar,
                CantidadPuertas = request.CantidadPuertas,
                CantidadAsientos = request.CantidadAsientos,
                Cilindrada = request.Cilindrada,
                Kilometraje = request.Kilometraje,
                Año = request.Año,
                Patente = request.Patente,
                Descripcion = request.Descripcion,
                Price = request.Precio
            };

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
