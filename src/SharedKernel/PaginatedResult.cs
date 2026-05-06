namespace SharedKernel;

/// <summary>
/// Generic paginated result for query responses.
/// </summary>
public record PaginatedResult<T>(
    List<T> Items,
    int TotalCount,
    int Page,
    int PageSize)
{
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}
