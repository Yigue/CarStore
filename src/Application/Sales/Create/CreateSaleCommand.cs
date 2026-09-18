using Application.Abstractions.Messaging;
using Domain.Financial.Attributes;
using Domain.Sales.Attributes;

namespace Application.Sales.Create;

public sealed record CreateSaleCommand(
    Guid CarId,
    Guid ClientId,
    // VEN-04: both become optional so a sale created FROM an accepted quote can inherit what was
    // actually agreed. Re-typing them into the sale form is how a vehicle gets invoiced at a
    // number nobody promised. A sale that is not converting a quote still has to state them —
    // the validator requires them in that case.
    decimal? FinalPrice,
    PaymentMethod? PaymentMethod,
    string ContractNumber,
    string Comments,
    Guid? LeadId = null,
    Guid? QuoteId = null,
    // Requested initial status. Null (or Pending) leaves the sale Pending — it is
    // only completed immediately when the caller explicitly asks for Completed.
    // Cancelled is rejected as an initial status by the validator.
    SaleStatus? Status = null,
    // Optional salesperson (User) who closed the sale.
    Guid? SalespersonId = null,

    // ─── VEN-02 · completing a prospect at invoicing time ────────────────────────────────────
    // A client the CRM invented when the lead reached Negociación carries a placeholder DNI and
    // no address. Enough to quote, not enough to bill. These two arrive with the sale and turn
    // that prospect into someone the dealership can invoice.
    string? ClientDni = null,
    string? ClientAddress = null,

    // ─── VEN-03 · how the operation is paid ──────────────────────────────────────────────────
    decimal? DownPayment = null,
    Guid? TradeInCarId = null,
    decimal? TradeInValue = null,
    decimal? FinancedAmount = null,
    int? InstallmentCount = null,
    decimal? InstallmentAmount = null,
    string? FinancingEntity = null,

    // ─── VEN-03 · paperwork numbers ──────────────────────────────────────────────────────────
    string? InvoiceNumber = null,
    string? TransferFormNumber = null,
    string? RegistrationNumber = null,
    DateTime? DeliveryDate = null
    ) : ICommand<Guid>;

