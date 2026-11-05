using Demeure.Application.Common.Queries;
using Microsoft.AspNetCore.Mvc;

namespace Demeure.Api.Contracts;

/// <summary>HTTP binding for the shared collection query; validation belongs to CollectionQuery.</summary>
public sealed record CollectionParameters
{
    /// <summary>One-based page index; defaults to 1.</summary>
    [FromQuery(Name = "page")]
    public int? Page { get; init; }
    /// <summary>Items per page, from 1 to 100; defaults to 20.</summary>
    [FromQuery(Name = "pageSize")]
    public int? PageSize { get; init; }
    /// <summary>Optional exact, case-sensitive status name for this resource.</summary>
    [FromQuery(Name = "status")]
    public string? Status { get; init; }
    /// <summary>id (default), createdAt, or status. Secondary ordering is always ascending id.</summary>
    [FromQuery(Name = "sortBy")]
    public string? SortBy { get; init; }
    /// <summary>asc (default) or desc.</summary>
    [FromQuery(Name = "sortDirection")]
    public string? SortDirection { get; init; }

    /// <summary>Converts bound HTTP parameters to the framework-independent query contract.</summary>
    public CollectionQuery ToQuery() => new()
    {
        Page = Page ?? 1, PageSize = PageSize ?? 20, Status = Status,
        SortBy = SortBy ?? "id", SortDirection = SortDirection ?? "asc"
    };
}
