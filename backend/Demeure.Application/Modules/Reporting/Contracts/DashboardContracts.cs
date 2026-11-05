using Demeure.Application.Common.Exceptions;

namespace Demeure.Application.Modules.Reporting.Contracts;

/// <summary>Inclusive financial reporting dates and optional authorized property selection.</summary>
public sealed record DashboardQuery(DateOnly StartDate, DateOnly EndDate, Guid? PropertyId)
{
    public void Validate()
    {
        var maximumEnd = StartDate.Year > 9994 ? DateOnly.MaxValue : StartDate.AddYears(5).AddDays(-1);
        if (StartDate == default || EndDate == default || EndDate < StartDate ||
            EndDate > maximumEnd || EndDate > DateOnly.MaxValue.AddDays(-31) ||
            PropertyId == Guid.Empty)
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["filters"] = ["Use valid inclusive dates spanning at most five calendar years and a valid optional property identifier."]
            });
    }
}

/// <summary>An authorized non-archived property available in the dashboard filter.</summary>
public sealed record DashboardPropertyResponse(Guid Id, string Name);
/// <summary>Amounts in a single currency; no conversion or cross-currency total is performed.</summary>
public sealed record DashboardMoneyResponse(string Currency, decimal RentalIncome, decimal OutstandingRent, decimal Expenses);
/// <summary>Actual completed rent payments and incurred approved/paid expenses for a calendar-month bucket.</summary>
public sealed record DashboardMonthResponse(DateOnly Month, IReadOnlyList<DashboardMoneyResponse> Amounts);
/// <summary>An active lease ending in the thirty-day window following the reporting end date.</summary>
public sealed record DashboardLeaseResponse(Guid Id, Guid PropertyId, string PropertyName, string UnitNumber, DateOnly EndDate);
/// <summary>A recent creation or latest update of an authorized record; not a complete audit history.</summary>
public sealed record DashboardActivityResponse(Guid Id, string Kind, string Label, string Status, DateTimeOffset OccurredAt);
/// <summary>Read-only landlord summary, current inventory and current states, with date-filtered finances and activity.</summary>
public sealed record DashboardResponse(
    Guid OrganizationId, DateOnly StartDate, DateOnly EndDate, Guid? PropertyId, DateTimeOffset GeneratedAt,
    IReadOnlyList<DashboardPropertyResponse> Properties,
    int TotalProperties, int TotalUnits, int OccupiedUnits, int VacantUnits, int UnavailableUnits,
    decimal? OccupancyRate, IReadOnlyList<DashboardMoneyResponse> Amounts,
    int PendingMaintenanceRequests, int ExpiringLeaseCount,
    IReadOnlyList<DashboardLeaseResponse> ExpiringLeases,
    IReadOnlyList<DashboardMonthResponse> Months, IReadOnlyList<DashboardActivityResponse> RecentActivities);
/// <summary>Provides live organization- and assigned-property-scoped landlord reporting.</summary>
public interface IDashboardReader
{
    Task<DashboardResponse> ReadAsync(DashboardQuery query, CancellationToken cancellationToken);
}
