namespace EventSourcing.Abstractions;

/// <summary>
/// Represents a paginated result set.
/// </summary>
/// <typeparam name="T">Type of the items in the result</typeparam>
public class PagedResult<T>
{
    /// <summary>
    /// Items in the current page.
    /// </summary>
    public IReadOnlyList<T> Items { get; init; }

    /// <summary>
    /// Current page number (1-based).
    /// </summary>
    public int PageNumber { get; init; }

    /// <summary>
    /// Number of items per page.
    /// </summary>
    public int PageSize { get; init; }

    /// <summary>
    /// Total number of items across all pages.
    /// </summary>
    public long TotalCount { get; init; }

    /// <summary>
    /// Total number of pages.
    /// </summary>
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;

    /// <summary>
    /// Whether there is a next page.
    /// </summary>
    public bool HasNextPage => PageNumber < TotalPages;

    /// <summary>
    /// Whether there is a previous page.
    /// </summary>
    public bool HasPreviousPage => PageNumber > 1;

    /// <summary>
    /// Constructor for PagedResult.
    /// </summary>
    /// <param name="items">Items in the current page</param>
    /// <param name="pageNumber">Current page number (1-based)</param>
    /// <param name="pageSize">Number of items per page</param>
    /// <param name="totalCount">Total number of items across all pages</param>
    public PagedResult(IEnumerable<T> items, int pageNumber, int pageSize, long totalCount)
    {
        Items = items.ToList().AsReadOnly();
        PageNumber = pageNumber;
        PageSize = pageSize;
        TotalCount = totalCount;
    }

    /// <summary>
    /// Create an empty paginated result.
    /// </summary>
    /// <param name="pageNumber">Current page number</param>
    /// <param name="pageSize">Number of items per page</param>
    /// <returns>Empty paginated result</returns>
    public static PagedResult<T> Empty(int pageNumber = 1, int pageSize = 10) =>
        new(Array.Empty<T>(), pageNumber, pageSize, 0);
}