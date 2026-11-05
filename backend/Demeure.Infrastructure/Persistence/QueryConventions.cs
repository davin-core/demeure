using System.Linq.Expressions;
using Demeure.Application.Common.Exceptions;
using Demeure.Application.Common.Queries;
using Demeure.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Demeure.Infrastructure.Persistence;

internal static class QueryConventions
{
    internal static async Task<PagedResponse<TResponse>> ReadAsync<TEntity, TStatus, TResponse>(
        IQueryable<TEntity> authorized, Expression<Func<TEntity, TStatus>> status,
        Expression<Func<TEntity, TResponse>> projection, Guid? id, CollectionQuery query,
        CancellationToken cancellationToken)
        where TEntity : Entity where TStatus : struct, Enum
    {
        var source = authorized.AsNoTracking();
        if (id.HasValue)
        {
            var items = await source.Where(value => value.Id == id.Value).Select(projection).ToListAsync(cancellationToken);
            return new PagedResponse<TResponse>(items, 1, 1, items.Count);
        }
        query.Validate();
        if (query.Status is not null)
        {
            if (!Enum.GetNames<TStatus>().Contains(query.Status, StringComparer.Ordinal) ||
                !Enum.TryParse<TStatus>(query.Status, out var parsed))
                throw new RequestValidationException(new Dictionary<string, string[]>
                    { ["status"] = [$"Status must be one of: {string.Join(", ", Enum.GetNames<TStatus>())}."] });
            var predicate = Expression.Lambda<Func<TEntity, bool>>(
                Expression.Equal(status.Body, Expression.Constant(parsed)), status.Parameters);
            source = source.Where(predicate);
        }
        var descending = query.SortDirection == "desc";
        var ordered = query.SortBy switch
        {
            "createdAt" => descending ? source.OrderByDescending(value => value.CreatedAt) : source.OrderBy(value => value.CreatedAt),
            "status" => descending ? source.OrderByDescending(status) : source.OrderBy(status),
            _ => descending ? source.OrderByDescending(value => value.Id) : source.OrderBy(value => value.Id)
        };
        if (query.SortBy != "id") ordered = ordered.ThenBy(value => value.Id);
        var count = await source.LongCountAsync(cancellationToken);
        var page = await ordered.Skip(checked((query.Page - 1) * query.PageSize)).Take(query.PageSize)
            .Select(projection).ToListAsync(cancellationToken);
        return new PagedResponse<TResponse>(page, query.Page, query.PageSize, count);
    }
}
