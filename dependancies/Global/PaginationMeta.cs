namespace dependancies.Global;

public class PaginationMeta : IMeta
{
    public static readonly string[] HEADERS = ["page", "page-size", "page-count", "total"];

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = -1;
    public int Limit { get; set; } = -1;
    public int PageCount => Limit <= 0 || Total <= 0 ? 1 : (int)Math.Ceiling(Total / (double)Limit);
    public int Total { get; set; } = -1;

    public Dictionary<string, string> GenerateDictionary() => new()
    {
        { "page", Page.ToString() },
        { "page-size", PageSize.ToString() },
        { "page-count", PageCount.ToString() },
        { "total", Total.ToString() }
    };
}
