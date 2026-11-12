namespace Application.Abstractions.Documents;

/// <summary>
/// Full legal content of the "boleto de compraventa" (sfc B3.2 / B8 / RN-028), built from
/// <c>Sale</c>, <c>Client</c>, <c>Car</c> and <c>DealerSettings</c>.
/// <para>
/// sfc B3.1 shipped this record with only <c>ContractNumber</c> and <c>LicensePlate</c>, just
/// to prove the rendering pipe end-to-end. Nothing besides the renderer and its own test
/// consumed that shape, so this PR replaces it outright with the real legal content instead of
/// adding a second model.
/// </para>
/// </summary>
public sealed record SaleContractDocument(
    string ContractNumber,
    string InvoiceNumber,
    string TransferFormNumber,
    string? RegistrationNumber,
    string LicensePlate,
    string Brand,
    string Model,
    int Year,
    DateTime SaleDate,
    DateTime? DeliveryDate,
    decimal FinalPrice,
    string Currency,
    string PaymentMethod,
    decimal? DownPayment,
    decimal? TradeInValue,
    decimal? FinancedAmount,
    int? InstallmentCount,
    decimal? InstallmentAmount,
    string? FinancingEntity,
    string ClientFullName,
    string ClientDni,
    string ClientAddress,
    string DealerName,
    string? DealerAddress,
    string? DealerPhoneNumber);
