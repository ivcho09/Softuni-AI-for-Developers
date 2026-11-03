namespace TaskFlow.DTOs.Common;

/// <summary>
/// A single page of results together with the metadata a client needs to page through the rest.
/// </summary>
public record PagedResponse<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];

    /// <summary>The 1-based page number that was returned.</summary>
    /// <example>1</example>
    public int Page { get; init; }

    /// <summary>The requested page size.</summary>
    /// <example>20</example>
    public int PageSize { get; init; }

    /// <summary>Total number of records matching the filter, across all pages.</summary>
    /// <example>42</example>
    public int TotalCount { get; init; }

    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public bool HasPreviousPage => Page > 1;

    public bool HasNextPage => Page < TotalPages;
}
