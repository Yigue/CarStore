using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Cars;
using Domain.Clients;
using Domain.Quotes;
using Domain.Shared.ValueObjects;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Quotes.CreateInquiry;

internal sealed class CreateInquiryCommandHandler(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<CreateInquiryCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateInquiryCommand command, CancellationToken cancellationToken)
    {
        Guid dealerId;
        Car? car = null;

        // 1. Determinar el DealerId y el Vehículo (si existe)
        if (command.CarId.HasValue)
        {
            car = await context.Cars
                .SingleOrDefaultAsync(c => c.Id == command.CarId, cancellationToken);

            if (car is null)
            {
                return Result.Failure<Guid>(Error.NotFound("Car.NotFound", $"Vehículo con ID {command.CarId} no encontrado."));
            }
            dealerId = car.DealerId;
        }
        else
        {
            // Para consultas generales, tomamos el primer dealer configurado
            var settings = await context.DealerSettings.FirstOrDefaultAsync(cancellationToken);
            if (settings is null)
            {
                return Result.Failure<Guid>(Error.Failure("Dealer.NotConfigured", "No hay ninguna concesionaria configurada."));
            }
            dealerId = settings.DealerId;
        }

        // 2. Validar email (lanza DomainException si es inválido — el GlobalExceptionHandler lo mapea a 400).
        Email inquiryEmail;
        try
        {
            inquiryEmail = new Email(command.Email);
        }
        catch (DomainException ex)
        {
            return Result.Failure<Guid>(Error.Validation("Email.Invalid", ex.Message));
        }

        var client = await context.Clients
            .SingleOrDefaultAsync(c => c.Email == inquiryEmail, cancellationToken);

        if (client is null)
        {
            client = new Client(
                dealerId,
                command.FirstName,
                command.LastName,
                string.Empty,
                command.Email,
                command.Phone,
                string.Empty,
                dateTimeProvider.UtcNow);

            context.Clients.Add(client);
        }

        // 3. Crear la cotización (Inquiry)
        // REGLA: Si no hay CarId, es una consulta general. 
        // Como la entidad Quote actual REQUIERE un CarId (FK), por ahora 
        // lanzaremos error si no hay auto, o usaremos un auto "dummy" si existiera.
        // MEJOR: Si no hay CarId, solo creamos/actualizamos el Cliente como Lead.
        // Pero el usuario quiere "anclar" componentes, así que hagamos que Quote.CarId sea opcional en el futuro.
        // Por ahora, si es consulta general, solo devolvemos éxito tras crear el cliente.
        
        if (car == null)
        {
            await context.SaveChangesAsync(cancellationToken);
            return Result.Success(client.Id); // Devolvemos ID de cliente para consultas generales
        }

        var quote = new Quote(
            dealerId,
            car,
            client,
            null,
            car.Price.Amount,
            dateTimeProvider.UtcNow.AddDays(30),
            command.Comments,
            dateTimeProvider.UtcNow);

        context.Quotes.Add(quote);

        // 4. Persistir
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(quote.Id);
    }
}
