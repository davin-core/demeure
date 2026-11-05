using System.ComponentModel.DataAnnotations;
using Demeure.Application.Common.Exceptions;

namespace Demeure.Application.Common.Queries;

/// <summary>Allowlisted offset pagination, status filtering and deterministic sorting.</summary>
public sealed record CollectionQuery
{
    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;
    [Range(1, 100)]
    public int PageSize { get; init; } = 20;
    [MaxLength(32)]
    public string? Status { get; init; }
    [Required, RegularExpression("^(id|createdAt|status)$")]
    public string SortBy { get; init; } = "id";
    [Required, RegularExpression("^(asc|desc)$")]
    public string SortDirection { get; init; } = "asc";

    public void Validate()
    {
        var failures = new List<ValidationResult>();
        Validator.TryValidateObject(this, new ValidationContext(this), failures, validateAllProperties: true);
        var errors = failures.SelectMany(failure => failure.MemberNames.Select(member =>
            (Member: member, Message: failure.ErrorMessage ?? "Invalid query value.")))
            .GroupBy(failure => failure.Member).ToDictionary(group => group.Key,
                group => group.Select(failure => failure.Message).ToArray());
        if (Page > 0 && PageSize > 0 && ((long)Page - 1) * PageSize > int.MaxValue)
            errors[nameof(Page)] = ["The requested page exceeds the supported offset."];
        if (Status is not null && string.IsNullOrWhiteSpace(Status))
            errors[nameof(Status)] = ["A supplied status must not be blank."];
        if (errors.Count > 0) throw new RequestValidationException(errors);
    }
}

/// <summary>A page whose count and items include only authorized, filtered records.</summary>
public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, long TotalCount)
{
    public long TotalPages => TotalCount / PageSize + (TotalCount % PageSize == 0 ? 0 : 1);
}
