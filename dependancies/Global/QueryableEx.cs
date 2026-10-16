using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace dependancies.Global;

public static class QueryableEx
{
    public static IQueryable<T> Where<T>(this IQueryable<T> query, bool condition, Expression<Func<T, bool>> @where)
    {
        return condition ? query.Where(@where) : query;
    }

    public static async Task<PaginatedResult<T>> ToPaginatedAsync<T>(this IQueryable<T> query, Pagination pagination, CancellationToken ct = default)
    {
        var total = await query.CountAsync(ct);
        var items = await query.Skip(pagination.Skip).Take(pagination.Limit).ToListAsync(ct);

        return new PaginatedResult<T>
        {
            Items = items,
            Total = total,
            Pagination = pagination
        };
    }
}
