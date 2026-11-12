namespace Application.Abstractions.Documents;

/// <summary>
/// Pre-filled data sheet for Formulario 08 (sfc B3.2 / B8 / RN-028) — NOT the official DNRPA
/// transfer form, which carries security elements CarStore cannot reproduce. This is a plain
/// reference sheet with the same data (vehicle, buyer, seller, transfer form number), for the
/// dealer's own use while filling out the real form.
/// </summary>
public sealed record Formulario08DataSheet(
    string TransferFormNumber,
    string LicensePlate,
    string Brand,
    string Model,
    int Year,
    DateTime SaleDate,
    string SellerDealerName,
    string? SellerDealerAddress,
    string BuyerFullName,
    string BuyerDni,
    string BuyerAddress);
