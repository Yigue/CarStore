namespace Application.Sales.GenerateDocument;

/// <summary>
/// sfc B3.2 / B8: the generated document's identity, content hash and version. The hash is
/// computed over the rendered PDF bytes and returned to the caller for integrity verification;
/// it is not persisted on <c>Document</c> (no schema change in this PR — see apply-progress).
/// </summary>
public sealed record GenerateSaleDocumentResult(
    Guid DocumentId,
    string Sha256Hash,
    int Version);
