using Application.Abstractions.Authentication;
using Application.Abstractions.Tenancy;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Billing.Entitlements;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Cars.Events;
using Domain.Shared.ValueObjects;
using Domain.Users;
using Application.Abstractions.Caching;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SharedKernel;

namespace Application.Cars.Create;

internal sealed class CreateCarCommandHandler(
    IApplicationDbContext context,
    DbContext dbContext,
    IDateTimeProvider dateTimeProvider,
    ICachedBrandService cachedBrandService,
    ICachedModelService cachedModelService,
    ICurrentTenantService tenantService,
    IEntitlementService entitlementService
    )
    : ICommandHandler<CreateCarCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateCarCommand command, CancellationToken cancellationToken)
    {
        // In-memory DB providers (used by unit tests) don't support transactions. Relational providers
        // do; the transaction keeps the advisory-lock-guarded entitlement check and the insert atomic —
        // see ProvisionDealerCommandHandler and IEntitlementService.EnsureCanAddAsync.
        IDbContextTransaction? transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;

        Car car;

        try
        {
            // sfc A3.1 (A1, A21, RN-011): the plan limit is checked BEFORE any other validation.
            Result entitlementCheck = await entitlementService.EnsureCanAddAsync(
                tenantService.DealerId, PlanResource.Vehicles, cancellationToken);
            if (entitlementCheck.IsFailure)
            {
                if (transaction is not null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                }

                return Result.Failure<Guid>(entitlementCheck.Error);
            }

            CurrencyCode? priceCurrency = null;
            if (command.Currency is not null)
            {
                if (!CurrencyCode.TryParse(command.Currency, out CurrencyCode parsed))
                {
                    return Result.Failure<Guid>(Error.Validation(
                        "Currency.Unsupported", "Unsupported currency. Supported: ARS, USD."));
                }

                priceCurrency = parsed;
            }

            // sfc B7.4: only one of the caller's own suppliers.
            if (command.SupplierId is { } supplierId
                && !await context.Suppliers.AnyAsync(s => s.Id == supplierId && s.DealerId == tenantService.DealerId, cancellationToken))
            {
                return Result.Failure<Guid>(Domain.Suppliers.SupplierErrors.NotFound(supplierId));
            }

            // sfc C2.1: only one of the caller's own branches.
            if (command.BranchId is { } branchId
                && !await context.Branches.AnyAsync(b => b.Id == branchId && b.DealerId == tenantService.DealerId, cancellationToken))
            {
                return Result.Failure<Guid>(Domain.Branches.BranchErrors.NotFound(branchId));
            }

            // Validate unique license plate before insert within tenant
            var licensePlate = new Domain.Shared.ValueObjects.LicensePlate(command.Patente);
            var existingCar = await context.Cars
                .AnyAsync(c => (!tenantService.HasTenant || c.DealerId == tenantService.DealerId) && c.Patente == licensePlate, cancellationToken);

            if (existingCar)
            {
                return Result.Failure<Guid>(CarErrors.PatenteAlreadyExists(command.Patente));
            }

            // Usar servicio de caché para obtener marca
            var marcaDto = await cachedBrandService.GetByIdAsync(command.Marca, cancellationToken);

            if (marcaDto is null)
            {
                return Result.Failure<Guid>(CarErrors.AtributesInvalid());
            }

            // Usar servicio de caché para obtener modelo
            var modeloDto = await cachedModelService.GetByIdAsync(command.Modelo, cancellationToken);

            if (modeloDto is null)
            {
                return Result.Failure<Guid>(CarErrors.AtributesInvalid());
            }

            var marca = await context.Marca.SingleAsync(m => m.Id == command.Marca, cancellationToken);
            var modelo = await context.Modelo.SingleAsync(m => m.Id == command.Modelo, cancellationToken);

            car = new Car(
                tenantService.DealerId,
                marca,
                modelo,
                command.Color,
                command.CarType,
                command.CarStatus,
                command.ServiceCar,
                command.CantidadPuertas,
                command.CantidadAsientos,
                command.Cilindrada,
                command.Kilometraje,
                command.Anio,
                command.Patente,
                command.Descripcion,
                command.Price,
                dateTimeProvider.UtcNow,
                command.FuelType,
                command.Featured,
                command.Transmission,
                command.PurchaseCost,
                priceCurrency
                );

            car.AssignSupplier(command.SupplierId);
            car.AssignBranch(command.BranchId);
            context.Cars.Add(car);

            await context.SaveChangesAsync(cancellationToken);

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
        }
        catch (DbUpdateException)
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }

            return Result.Failure<Guid>(CarErrors.PatenteAlreadyExists(command.Patente));
        }
        catch
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }

            throw;
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }

        return Result.Success(car.Id);
    }
}
