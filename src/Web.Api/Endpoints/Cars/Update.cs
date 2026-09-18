using Application.Cars.Update;
using Domain.Cars;
using Domain.Cars.Attributes;
using MediatR;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Cars;

internal sealed class Update : IEndpoint
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
        public int FuelType { get; set; }
        public bool Featured { get; set; }
        public int Transmission { get; set; }
        public decimal? PurchaseCost { get; set; }

        /// <summary>
        /// INV-01: which of the car's images becomes the cover. Optional — omit it (or send null)
        /// to leave the gallery exactly as it is.
        /// </summary>
        public Guid? CoverImageId { get; set; }
    }

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("cars/{id:guid}", async (Guid id, Request request, ISender sender, CancellationToken cancellationToken) =>
        {
            // Named arguments (ArchitectureTests.CommandConstructionTests). Five of these are
            // plain ints — puertas, asientos, cilindrada, kilometraje, año — and positionally a
            // swapped pair compiles and ships a 2021-door car with 4 as its model year. This
            // call site was grandfathered into the allowlist by line number; adding CoverImageId
            // moved it, and re-pinning the number would have kept the hazard instead of the rule.
            var command = new UpdateCarCommand(
                Id: id,
                Marca: request.Marca,
                Modelo: request.Modelo,
                Color: (Color)request.Color,
                CarType: (TypeCar)request.CarType,
                CarStatus: (StatusCar)request.CarStatus,
                ServiceCar: (StatusServiceCar)request.ServiceCar,
                CantidadPuertas: request.CantidadPuertas,
                CantidadAsientos: request.CantidadAsientos,
                Cilindrada: request.Cilindrada,
                Kilometraje: request.Kilometraje,
                Anio: request.Anio,
                Patente: request.Patente,
                Descripcion: request.Descripcion,
                Price: request.Precio,
                FuelType: (FuelType)request.FuelType,
                Featured: request.Featured,
                Transmission: (Transmission)request.Transmission,
                PurchaseCost: request.PurchaseCost,
                CoverImageId: request.CoverImageId
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
