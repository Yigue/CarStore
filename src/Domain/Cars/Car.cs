using Domain.Cars.Attributes;
using Domain.Cars.Events;
using Domain.Shared.ValueObjects;
using SharedKernel;

namespace Domain.Cars;

public sealed class Car : Entity
{
    public Guid MarcaId { get; private set; }
    public Marca Marca { get; private set; }
    public Guid ModeloId { get; private set; }
    public Modelo Modelo { get; private set; }
    public Color Color { get; private set; }
    public TypeCar CarType { get; private set; }
    public StatusCar CarStatus { get; private set; }
    public StatusServiceCar ServiceCar { get; private set; }
    public FuelType FuelType { get; private set; }
    public Transmission Transmission { get; private set; }
    public bool Featured { get; private set; }
    public int CantidadPuertas { get; private set; }
    public int CantidadAsientos { get; private set; }
    public int Cilindrada { get; private set; }
    public int Kilometraje { get; private set; }
    public int Anio { get; private set; }
    public LicensePlate Patente { get; private set; }
    public string Descripcion { get; private set; }
    public ICollection<CarImage> Images { get; private set; } = new List<CarImage>();

    public DateTime CreatedAt { get; private set; }
    public Money Price { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    // PHASE-4: PurchaseCost is nullable — existing Cars in DB have no value until backfilled.
    // It represents the cost the dealership paid for the unit and is the base for TCO.
    public Money? PurchaseCost { get; private set; }

    /// <summary>
    /// Withdrawn from circulation but still referenced by commercial history. Filtered out of
    /// every query by the global filter in <c>ApplicationDbContext</c>, exactly like Client
    /// and Quote. See <see cref="SoftDelete"/>.
    /// </summary>
    public bool IsDeleted { get; private set; }

    public DateTime? DeletedAtUtc { get; private set; }

    private readonly List<ReconditioningTask> _reconditioningTasks = [];
    public IReadOnlyList<ReconditioningTask> ReconditioningTasks => _reconditioningTasks.AsReadOnly();


    private Car()
    {
        Images = new List<CarImage>();
    }
    
    public Car(
        Guid dealerId,
        Marca marca,
        Modelo modelo,
        Color color,
        TypeCar carType,
        StatusCar carStatus,
        StatusServiceCar serviceCar,
        int cantidadPuertas,
        int cantidadAsientos,
        int cilindrada,
        int kilometraje,
        int anio,
        string patente,
        string descripcion,
        decimal price,
        DateTime date,
        FuelType fuelType = FuelType.Gasolina,
        bool featured = false,
        Transmission transmission = Transmission.Manual,
        decimal? purchaseCost = null
        )
    {
        SetDealer(dealerId);
        Id = Guid.NewGuid();
        Marca = marca;
        MarcaId = marca.Id;
        Modelo = modelo;
        ModeloId = modelo.Id;
        Color = color;
        CarType = carType;
        CarStatus = carStatus;
        ServiceCar = serviceCar;
        FuelType = fuelType;
        Transmission = transmission;
        Featured = featured;
        CantidadPuertas = cantidadPuertas;
        CantidadAsientos = cantidadAsientos;
        Cilindrada = cilindrada;
        Kilometraje = kilometraje;
        Anio = anio;
        Patente = new LicensePlate(patente);
        Descripcion = descripcion;
        Price = Money.Of(price, CurrencyCode.Ars);

        if (purchaseCost.HasValue)
            PurchaseCost = Money.Of(purchaseCost.Value, CurrencyCode.Ars);

        CreatedAt = date;
        UpdatedAt = date;

        Raise(new NewCarDomainEvent(Id));
    }

    public void UpdateDetails(
        Marca marca,
        Modelo modelo,
        Color color,
        TypeCar carType,
        StatusCar carStatus,
        StatusServiceCar serviceCar,
        int cantidadPuertas,
        int cantidadAsientos,
        int cilindrada,
        int kilometraje,
        int anio,
        string patente,
        string descripcion,
        DateTime updatedAt,
        FuelType fuelType = FuelType.Gasolina,
        bool featured = false,
        Transmission transmission = Transmission.Manual,
        decimal? purchaseCost = null)
    {
        Marca = marca;
        MarcaId = marca.Id;
        Modelo = modelo;
        ModeloId = modelo.Id;
        Color = color;
        CarType = carType;
        CarStatus = carStatus;
        ServiceCar = serviceCar;
        FuelType = fuelType;
        Transmission = transmission;
        Featured = featured;
        CantidadPuertas = cantidadPuertas;
        CantidadAsientos = cantidadAsientos;
        Cilindrada = cilindrada;
        Kilometraje = kilometraje;
        Anio = anio;
        Patente = new LicensePlate(patente);
        Descripcion = descripcion;
        if (purchaseCost.HasValue)
            PurchaseCost = Money.Of(purchaseCost.Value, CurrencyCode.Ars);
        UpdatedAt = updatedAt;
    }
    
    public void MarkAsSold(DateTime updatedAt)
    {
        if (ServiceCar == StatusServiceCar.Vendido)
            return;
        
        ServiceCar = StatusServiceCar.Vendido;
        UpdatedAt = updatedAt;
        Raise(new CarSoldDomainEvent(Id));
    }
    
    public void MarkAsAvailable(DateTime updatedAt)
    {
        if (ServiceCar == StatusServiceCar.Disponible)
            return;

        ServiceCar = StatusServiceCar.Disponible;
        UpdatedAt = updatedAt;
    }

    /// <summary>
    /// D-1: reserva el vehículo para una cotización activa. Solo un vehículo
    /// Disponible puede reservarse; cualquier otro estado lanza NotAvailable.
    /// </summary>
    public void Reserve(DateTime updatedAt)
    {
        if (ServiceCar != StatusServiceCar.Disponible)
            throw new DomainException($"El vehículo '{Id}' no está disponible para reservar");

        ServiceCar = StatusServiceCar.Reservado;
        UpdatedAt = updatedAt;
    }

    /// <summary>
    /// D-1: libera una reserva (al rechazar o expirar la cotización). Idempotente:
    /// si el vehículo no está Reservado no hace nada (p. ej. ya se vendió).
    /// </summary>
    public void Release(DateTime updatedAt)
    {
        if (ServiceCar != StatusServiceCar.Reservado)
            return;

        ServiceCar = StatusServiceCar.Disponible;
        UpdatedAt = updatedAt;
    }

    /// <summary>
    /// sfc B1 / ADR C3 (RN-005, RN-006): guarded status change following <see cref="VehicleLifecycle"/>.
    /// Vendido can never be left through here (only <see cref="RevertSale"/>, or MarkDelivered
    /// for Entregado). Same-status is an idempotent success. Existing callers of
    /// MarkAsSold/MarkAsAvailable/Reserve/Release are unchanged.
    /// </summary>
    public Result ChangeStatus(StatusServiceCar target, DateTime updatedAt)
    {
        if (VehicleLifecycle.Normalize(ServiceCar) == VehicleLifecycle.Normalize(target))
            return Result.Success();

        if (ServiceCar == StatusServiceCar.Vendido && target != StatusServiceCar.Entregado)
            return Result.Failure(CarErrors.SaleActiveBlocksStatusChange(Id));

        if (!VehicleLifecycle.CanTransition(ServiceCar, target))
            return Result.Failure(CarErrors.InvalidStatusTransition(Id, ServiceCar, target));

        StatusServiceCar normalizedTarget = VehicleLifecycle.Normalize(target);
        ServiceCar = normalizedTarget;
        UpdatedAt = updatedAt;

        if (normalizedTarget == StatusServiceCar.Vendido)
            Raise(new CarSoldDomainEvent(Id));

        return Result.Success();
    }

    /// <summary>RN-005: Vendido → Entregado once the delivery is confirmed.</summary>
    public Result MarkDelivered(DateTime updatedAt) => ChangeStatus(StatusServiceCar.Entregado, updatedAt);

    /// <summary>
    /// RN-006 (ADR C3): the ONLY way back from Vendido to Disponible. Invoked when a sale is
    /// voided (<c>SaleVoidedDomainEvent</c>); a plain status edit is rejected by <see cref="ChangeStatus"/>.
    /// </summary>
    public Result RevertSale(Guid saleId, DateTime updatedAt)
    {
        if (ServiceCar != StatusServiceCar.Vendido)
            return Result.Failure(CarErrors.NotSold(Id));

        ServiceCar = StatusServiceCar.Disponible;
        UpdatedAt = updatedAt;
        return Result.Success();
    }

    /// <summary>
    /// D-1: concreta la venta (Reservado o Disponible -> Vendido). Idempotente si ya está vendido.
    /// </summary>
    public void Sell(DateTime updatedAt) => MarkAsSold(updatedAt);
    
    public void UpdatePrice(decimal newPrice, DateTime updatedAt)
    {
        Price = Money.Of(newPrice, CurrencyCode.Ars);
        UpdatedAt = updatedAt;
    }
    
    public void UpdatePrice(Money newPrice, DateTime updatedAt)
    {
        Price = newPrice;
        UpdatedAt = updatedAt;
    }

    /// <summary>
    /// Sets or updates the purchase cost of the vehicle.
    /// </summary>
    public void SetPurchaseCost(Money purchaseCost, DateTime updatedAt)
    {
        ArgumentNullException.ThrowIfNull(purchaseCost);
        PurchaseCost = purchaseCost;
        UpdatedAt = updatedAt;
    }

    /// <summary>
    /// Adds a new pending reconditioning task to this car and returns it.
    /// </summary>
    public ReconditioningTask AddReconditioningTask(string description, Money cost)
    {
        var task = ReconditioningTask.Create(DealerId, Id, description, cost);
        _reconditioningTasks.Add(task);
        return task;
    }

    /// <summary>
    /// Completes the reconditioning task with the given id and bubbles its domain event.
    /// Throws <see cref="DomainException"/> when no task with that id is part of the aggregate.
    /// </summary>
    public void CompleteReconditioningTask(Guid taskId, DateTime completedAtUtc)
    {
        ReconditioningTask? task = _reconditioningTasks.FirstOrDefault(t => t.Id == taskId)
            ?? throw new DomainException(
                $"ReconditioningTask '{taskId}' was not found on Car '{Id}'");

        task.Complete(completedAtUtc);
    }

    /// <summary>
    /// Total Cost of Ownership: <see cref="PurchaseCost"/> + sum of cost of all completed
    /// reconditioning tasks (currencies must match). When no purchase cost is set we use
    /// <see cref="Money.Zero"/> defaulted to the currency of completed tasks (or <see cref="Money.DefaultCurrency"/>).
    /// </summary>
    public Money GetTotalCostOfOwnership()
    {
        List<ReconditioningTask> completed = _reconditioningTasks
            .Where(t => t.Status == ReconditioningStatus.Completed)
            .ToList();

        string baseCurrency = PurchaseCost?.Currency
            ?? completed.FirstOrDefault()?.Cost.Currency
            ?? Money.DefaultCurrency;

        Money total = PurchaseCost ?? new Money(0, baseCurrency);

        foreach (ReconditioningTask task in completed)
        {
            total += task.Cost;
        }

        return total;
    }

    /// <summary>
    /// Withdraws the vehicle from circulation without destroying it. Idempotent.
    /// <para>
    /// Five foreign keys — Appointment, FinancialTransaction, Lead, Quote and Sale — reference
    /// a Car with <c>DeleteBehavior.Restrict</c>, because each of those rows is commercial
    /// history that outlives the unit (see <c>CarReferenceDeleteBehaviorTests</c>). A vehicle
    /// anyone has ever quoted therefore cannot be physically removed, and must not be: the row
    /// is what a sale, a transaction or a lead still points at.
    /// </para>
    /// <para>
    /// <see cref="DeleteCarCommandHandler"/> picks between this and physical deletion by
    /// counting those references, so REQ-VMS-5 / ADR-5 still governs the clean case.
    /// </para>
    /// </summary>
    public void SoftDelete(DateTime occurredAtUtc)
    {
        if (IsDeleted)
        {
            return;
        }

        IsDeleted = true;
        DeletedAtUtc = occurredAtUtc;
        UpdatedAt = occurredAtUtc;

        Raise(new CarDeleteDomainEvent(Id));
    }

    /// <summary>
    /// Returns a withdrawn vehicle to circulation. Idempotent — restoring a live vehicle is a
    /// no-op rather than an error, so a retried request cannot fail spuriously.
    /// </summary>
    public void Restore(DateTime occurredAtUtc)
    {
        if (!IsDeleted)
        {
            return;
        }

        IsDeleted = false;
        DeletedAtUtc = null;
        UpdatedAt = occurredAtUtc;
    }
}
