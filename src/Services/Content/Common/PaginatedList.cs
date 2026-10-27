using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;

namespace Content.Common;

public class PaginatedList<T>
{
    [JsonPropertyName("items")]
    public IReadOnlyCollection<T> Items { get; set; } = [];

    [JsonPropertyName("pageNumber")]
    public int PageNumber { get; set; }

    [JsonPropertyName("totalPage")]
    public int TotalPage { get; set; }

    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }

    public PaginatedList() { }

    [JsonConstructor]
    public PaginatedList(IReadOnlyCollection<T> items, int pageNumber, int totalPage, int totalCount)
    {
        Items = items;
        PageNumber = pageNumber;
        TotalPage = totalPage;
        TotalCount = totalCount;
    }

    public PaginatedList(IReadOnlyCollection<T> items, int count, int pageNumber, int pageSize, bool _)
    {
        PageNumber = pageNumber;
        TotalPage = pageSize > 0 ? (int)Math.Ceiling(count / (double)pageSize) : 0;
        TotalCount = count;
        Items = items;
    }

    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPage;

    public static async Task<PaginatedList<T>> CreateAsync(
        IQueryable<T> source, int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var count = await source.CountAsync(ct);
        var items = await source.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        return new PaginatedList<T>(items, count, pageNumber, pageSize, true);
    }
}
