using Application.Cars.Create;
using Domain.Cars;
using Domain.Cars.Atribbutes;
using MediatR;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;
using Web.Api.Endpoints.Cars;

namespace Web.Api.Endpoints.Cars;

internal sealed class Create : IEndpoint
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
        app.MapPost("cars", async (Request request, ISender sender, CancellationToken cancellationToken) =>
        {
            // Validar enums
            if(!Enum.IsDefined(typeof(Color), request.Color))
            {

                return Results.BadRequest("Color inválido");
            }
            if(!Enum.IsDefined(typeof(TypeCar), request.CarType))
            {

                return Results.BadRequest("Tipo de carro inválido");
            }
            if(!Enum.IsDefined(typeof(StatusCar), request.CarStatus))
            {

                return Results.BadRequest("Estado de carro inválido");
            }
            if (!Enum.IsDefined(typeof(statusServiceCar), request.ServiceCar))
            {

                return Results.BadRequest("Estado de servicio inválido");
            }

            try 
            {
                var command = new CreateCarCommand
                {
                    Marca = new Guid(request.Marca),
                    Modelo = new Guid(request.Modelo),
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
            }
            catch (FormatException)
            {
                return Results.BadRequest("GUID inválido para Marca o Modelo");
            }
            catch (Exception ex)
            {
                return Results.Problem(ex.Message);
            }
        })
        .HasPermission(Permissions.CarsCreate)
        .WithTags(Tags.Cars)
        .WithName("CreateCar")
        .Produces<Guid>(StatusCodes.Status201Created)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
