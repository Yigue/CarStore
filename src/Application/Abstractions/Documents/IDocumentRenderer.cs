namespace Application.Abstractions.Documents;

/// <summary>
/// Renders a model into document bytes (PDF) for a named <see cref="DocumentTemplate"/>.
/// sfc B3.1 / B8: Application depends only on this abstraction; the rendering engine
/// (QuestPDF) is an Infrastructure concern and never leaks here.
/// </summary>
public interface IDocumentRenderer
{
    Task<byte[]> RenderAsync<TModel>(
        DocumentTemplate template,
        TModel model,
        CancellationToken cancellationToken = default);
}
