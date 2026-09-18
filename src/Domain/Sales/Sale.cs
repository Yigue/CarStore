using SharedKernel;
using Domain.Cars;
using Domain.Clients;
using Domain.Sales.Attributes;
using Domain.Financial;
using Domain.Financial.Attributes;
using Domain.Sales.Events;
using Domain.Shared.ValueObjects;

namespace Domain.Sales;

public sealed class Sale : Entity
{
    public Guid CarId { get; private set; }
    public Car Car { get; private set; }
    public Guid ClientId { get; private set; }
    public Client Client { get; private set; }
    public Guid? QuoteId { get; private set; }
    public Guid? LeadId { get; private set; }
    public Guid? SalespersonId { get; private set; }
    public Money FinalPrice { get; private set; }
    public SaleStatus Status { get; private set; }
    public PaymentMethod PaymentMethod { get; private set; }
    public string ContractNumber { get; private set; }
    public DateTime SaleDate { get; private set; }
    public string Comments { get; private set; }

    // ─── VEN-03 · Cómo se paga ───────────────────────────────────────────────────────────────
    //
    // Hasta acá una venta sabía UN precio y UNA forma de pago, que es como se registra un pago
    // al contado y nada más. Una concesionaria vende casi siempre de otra manera: una seña, un
    // usado tomado en parte de pago, y el resto financiado en cuotas por una entidad. Sin esos
    // datos la venta no explica de dónde sale su propio total, y la cobranza vive en un Excel.
    //
    // Todos nullable: una venta cargada antes de este cambio, o cargada al contado, no tiene
    // nada que completar acá.

    /// <summary>Anticipo entregado al cerrar la operación.</summary>
    public Money? DownPayment { get; private set; }

    /// <summary>Vehículo que el comprador entrega en parte de pago, si hay.</summary>
    public Guid? TradeInCarId { get; private set; }

    /// <summary>Valor acordado por el usado tomado en parte de pago.</summary>
    public Money? TradeInValue { get; private set; }

    /// <summary>Monto financiado — el saldo que queda después de la seña y la permuta.</summary>
    public Money? FinancedAmount { get; private set; }

    /// <summary>Cantidad de cuotas del monto financiado.</summary>
    public int? InstallmentCount { get; private set; }

    /// <summary>Importe de cada cuota.</summary>
    public Money? InstallmentAmount { get; private set; }

    /// <summary>Entidad que otorga la financiación (banco, financiera, plan de ahorro).</summary>
    public string? FinancingEntity { get; private set; }

    // ─── VEN-03 · Documentación legal ────────────────────────────────────────────────────────
    //
    // Los archivos adjuntos ya existen (Document.SaleId), pero un archivo no es un número: la
    // factura, el formulario 08 y el patentamiento se buscan por su número, y hasta acá ese
    // número no vivía en ninguna parte del sistema.

    /// <summary>Número de factura emitida por la operación.</summary>
    public string? InvoiceNumber { get; private set; }

    /// <summary>Número del formulario de transferencia (08) presentado.</summary>
    public string? TransferFormNumber { get; private set; }

    /// <summary>Número o constancia de patentamiento / inscripción registral.</summary>
    public string? RegistrationNumber { get; private set; }

    /// <summary>Fecha de entrega efectiva de la unidad al comprador.</summary>
    public DateTime? DeliveryDate { get; private set; }

    // Private parameterless constructor for EF Core
    private Sale()
    {
    
    }

    public Sale(
        Guid dealerId,
        Guid carId,
        Guid clientId,
        decimal finalPrice,
        PaymentMethod paymentMethod,
        string contractNumber,
        string comments,
        DateTime saleDate,
        Guid? leadId = null,
        Guid? quoteId = null,
        Guid? salespersonId = null)
    {
        SetDealer(dealerId);
        Id = Guid.NewGuid();
        CarId = carId;
        ClientId = clientId;
        LeadId = leadId;
        QuoteId = quoteId;
        SalespersonId = salespersonId;
        FinalPrice = new Money(finalPrice);
        PaymentMethod = paymentMethod;
        ContractNumber = contractNumber;
        Comments = comments;
        Status = SaleStatus.Pending;
        SaleDate = saleDate;

        Raise(new SaleCreatedDomainEvent(Id, CarId, ClientId, FinalPrice, LeadId));
    }

    public void Complete()
    {
        if (Status != SaleStatus.Pending)
            throw new DomainException("Only pending sales can be completed");
        
        Status = SaleStatus.Completed;
        Raise(new SaleCompletedDomainEvent(Id, CarId, ClientId, FinalPrice, PaymentMethod));
    }

    public void Cancel(string reason)
    {
        if (Status != SaleStatus.Pending)
            throw new DomainException("Only pending sales can be cancelled");
        
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("Cancellation reason is required");
        
        Status = SaleStatus.Cancelled;
        Raise(new SaleCancelledDomainEvent(Id, CarId, reason));
    }
    
    public void Update(
        decimal finalPrice,
        PaymentMethod paymentMethod,
        string contractNumber,
        string comments)
    {
        if (Status != SaleStatus.Pending)
            throw new DomainException("Only pending sales can be updated");
        
        FinalPrice = new Money(finalPrice);
        PaymentMethod = paymentMethod;
        ContractNumber = contractNumber;
        Comments = comments;
    }
    
    public void Update(
        Money finalPrice,
        PaymentMethod paymentMethod,
        string contractNumber,
        string comments)
    {
        if (Status != SaleStatus.Pending)
            throw new DomainException("Only pending sales can be updated");
        
        FinalPrice = finalPrice;
        PaymentMethod = paymentMethod;
        ContractNumber = contractNumber;
        Comments = comments;
    }

    /// <summary>
    /// Assigns (or reassigns) the salesperson who closed this sale. Nullable — a sale
    /// can be recorded without a salesperson on file.
    /// </summary>
    /// <summary>
    /// VEN-03: records how the operation is actually paid.
    ///
    /// <para>
    /// The parts have to add up to the price. A sale whose seña + permuta + financiado does not
    /// equal its FinalPrice is not a rounding problem, it is a number nobody can collect against,
    /// and letting it through means the discrepancy surfaces months later in the cobranza. The
    /// check only runs once every part is present: loading a sale in stages is normal, and a
    /// half-filled form must not be refused for not adding up yet.
    /// </para>
    /// </summary>
    public Result SetPaymentTerms(
        decimal? downPayment,
        Guid? tradeInCarId,
        decimal? tradeInValue,
        decimal? financedAmount,
        int? installmentCount,
        decimal? installmentAmount,
        string? financingEntity)
    {
        if (downPayment is < 0 || tradeInValue is < 0 || financedAmount is < 0 || installmentAmount is < 0)
        {
            return Result.Failure(SalesErrors.NegativePaymentAmount());
        }

        if (tradeInValue is > 0 && tradeInCarId is null)
        {
            return Result.Failure(SalesErrors.TradeInValueWithoutVehicle());
        }

        if (financedAmount is > 0 && (installmentCount is null or <= 0))
        {
            return Result.Failure(SalesErrors.FinancingWithoutInstallments());
        }

        // Every part declared → the total has to be the price.
        if (downPayment.HasValue && financedAmount.HasValue)
        {
            decimal parts = downPayment.Value + financedAmount.Value + (tradeInValue ?? 0m);
            if (Math.Abs(parts - FinalPrice.Amount) > 0.01m)
            {
                return Result.Failure(SalesErrors.PaymentPartsDoNotMatchPrice(FinalPrice.Amount, parts));
            }
        }

        DownPayment = downPayment.HasValue ? new Money(downPayment.Value) : null;
        TradeInCarId = tradeInCarId;
        TradeInValue = tradeInValue.HasValue ? new Money(tradeInValue.Value) : null;
        FinancedAmount = financedAmount.HasValue ? new Money(financedAmount.Value) : null;
        InstallmentCount = installmentCount;
        InstallmentAmount = installmentAmount.HasValue ? new Money(installmentAmount.Value) : null;
        FinancingEntity = string.IsNullOrWhiteSpace(financingEntity) ? null : financingEntity.Trim();

        return Result.Success();
    }

    /// <summary>
    /// VEN-03: records the paperwork numbers. Attachments already existed
    /// (<c>Document.SaleId</c>), but a file is not a number — the factura, the 08 and the
    /// patentamiento are looked up by theirs, and until now that number lived nowhere.
    /// </summary>
    public void SetLegalDocuments(
        string? invoiceNumber,
        string? transferFormNumber,
        string? registrationNumber,
        DateTime? deliveryDate)
    {
        InvoiceNumber = Normalize(invoiceNumber);
        TransferFormNumber = Normalize(transferFormNumber);
        RegistrationNumber = Normalize(registrationNumber);
        DeliveryDate = deliveryDate;

        static string? Normalize(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    public void AssignSalesperson(Guid? salespersonId)
    {
        SalespersonId = salespersonId;
    }
}
