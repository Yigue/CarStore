namespace Domain.Deposits;

/// <summary>Seña lifecycle: Activa → Vencida / Reembolsada / Aplicada a venta (RN-001).</summary>
public enum DepositStatus
{
    Active,
    Expired,
    Refunded,
    Applied
}
