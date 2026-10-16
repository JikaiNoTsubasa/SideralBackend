namespace dependancies.Global;

public class Pagination
{
    public const int DEFAULT_LIMIT = 20;
    public const int MAX_LIMIT = 200;

    public int Page { get; set; } = 1;
    public int Limit { get; set; } = DEFAULT_LIMIT;

    public int Skip => (Math.Max(Page, 1) - 1) * Limit;
}

public class PaginatedResult<T>
{
    public required List<T> Items { get; init; }
    public required int Total { get; init; }
    public required Pagination Pagination { get; init; }

    public PaginationMeta Meta => new()
    {
        Page = Pagination.Page,
        Limit = Pagination.Limit,
        PageSize = Items.Count,
        Total = Total
    };
}
