using Demeure.Application.Common;
using Demeure.Application.Common.Exceptions;
using Demeure.Application.Modules.Reporting.Contracts;
using Demeure.Application.Modules.Users.Contracts;
using Demeure.Domain.Modules.Expenses.Entities;
using Demeure.Domain.Modules.Leases.Entities;
using Demeure.Domain.Modules.Maintenance.Entities;
using Demeure.Domain.Modules.Payments.Entities;
using Demeure.Domain.Modules.Properties.Entities;
using Demeure.Domain.Modules.Units.Entities;
using Demeure.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Demeure.Infrastructure.Authorization;

internal sealed class DashboardReader(DemeureDbContext db, IOrganizationContext context,
    IAuthenticationService authentication, OrganizationDataTransaction transactions) : IDashboardReader
{
    public async Task<DashboardResponse> ReadAsync(DashboardQuery query, CancellationToken cancellationToken)
    {
        query.Validate();
        var session = await authentication.GetSessionAsync(cancellationToken);
        var membership = session?.Memberships.SingleOrDefault(value => value.OrganizationId == context.OrganizationId);
        if (session is null || membership?.Role is not (ApplicationRoles.OrganizationOwner or ApplicationRoles.PropertyManager))
            throw new ForbiddenException("An owner or property manager membership is required.");
        await using var transaction = await transactions.BeginAsync(session.UserId, false, cancellationToken, consistentSnapshot: true);
        var owner = membership.Role == ApplicationRoles.OrganizationOwner;
        var assigned = db.PropertyManagerAssignments.Where(value => value.UserId == session.UserId).Select(value => value.PropertyId);
        var readable = db.Properties.AsNoTracking().Where(value => value.Status != PropertyStatus.Archived && (owner || assigned.Contains(value.Id)));
        var choices = await readable.OrderBy(value => value.Name).ThenBy(value => value.Id)
            .Select(value => new DashboardPropertyResponse(value.Id, value.Name)).ToListAsync(cancellationToken);
        if (query.PropertyId is { } propertyId && !choices.Any(value => value.Id == propertyId))
            throw new ResourceNotFoundException("The requested property is unavailable.");
        var properties = readable.Where(value => query.PropertyId == null || value.Id == query.PropertyId);
        var propertyIds = properties.Select(value => value.Id);
        var units = db.Units.AsNoTracking().Where(value => propertyIds.Contains(value.PropertyId) && value.Status != UnitStatus.Archived);
        // Financial and historical lease records retain links through archived units.
        var scopedUnits = db.Units.Where(value => propertyIds.Contains(value.PropertyId));
        var unitIds = scopedUnits.Select(value => value.Id);
        var leases = db.Leases.AsNoTracking().Where(value => unitIds.Contains(value.UnitId));
        var leaseIds = leases.Select(value => value.Id);
        var payments = db.Payments.AsNoTracking().Where(value => leaseIds.Contains(value.LeaseId));
        var expenses = db.Expenses.AsNoTracking().Where(value => propertyIds.Contains(value.PropertyId));
        var maintenance = db.MaintenanceRequests.AsNoTracking().Where(value => propertyIds.Contains(value.PropertyId));
        var start = new DateTimeOffset(query.StartDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var end = new DateTimeOffset(query.EndDate.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var totalProperties = await properties.CountAsync(cancellationToken);
        var totalUnits = await units.CountAsync(cancellationToken);
        var occupied = await units.CountAsync(value => value.Status == UnitStatus.Occupied, cancellationToken);
        var vacant = await units.CountAsync(value => value.Status == UnitStatus.Available, cancellationToken);
        var unavailable = await units.CountAsync(value => value.Status == UnitStatus.Unavailable, cancellationToken);
        var pending = await maintenance.CountAsync(value => value.Status == MaintenanceStatus.Open ||
            value.Status == MaintenanceStatus.Assigned || value.Status == MaintenanceStatus.InProgress, cancellationToken);
        var outstanding = await payments.Where(value => (value.Status == PaymentStatus.Pending || value.Status == PaymentStatus.Failed) &&
                value.DueDate <= query.EndDate)
            .GroupBy(value => value.Currency).Select(group => new Amount(group.Key, group.Sum(value => value.Amount))).ToListAsync(cancellationToken);
        var months = new List<DashboardMonthResponse>();
        var totals = new Dictionary<string, DashboardMoneyResponse>(StringComparer.Ordinal);
        foreach (var value in outstanding) totals[value.Currency] = new(value.Currency, 0, value.Value, 0);
        for (var month = new DateOnly(query.StartDate.Year, query.StartDate.Month, 1); month <= query.EndDate; month = month.AddMonths(1))
        {
            var bucketStart = month < query.StartDate ? query.StartDate : month;
            var next = month.AddMonths(1);
            var bucketEnd = next > query.EndDate ? query.EndDate.AddDays(1) : next;
            var instantStart = new DateTimeOffset(bucketStart.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            var instantEnd = new DateTimeOffset(bucketEnd.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            var income = await payments.Where(value => value.Status == PaymentStatus.Completed &&
                    value.PaidAt >= instantStart && value.PaidAt < instantEnd)
                .GroupBy(value => value.Currency).Select(group => new Amount(group.Key, group.Sum(value => value.Amount))).ToListAsync(cancellationToken);
            var costs = await expenses.Where(value => (value.Status == ExpenseStatus.Approved || value.Status == ExpenseStatus.Paid) &&
                    value.IncurredDate >= bucketStart && value.IncurredDate < bucketEnd)
                .GroupBy(value => value.Currency).Select(group => new Amount(group.Key, group.Sum(value => value.Amount))).ToListAsync(cancellationToken);
            var bucket = income.Select(value => value.Currency).Concat(costs.Select(value => value.Currency)).Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal).Select(currency => new DashboardMoneyResponse(currency,
                    income.Where(value => value.Currency == currency).Sum(value => value.Value), 0,
                    costs.Where(value => value.Currency == currency).Sum(value => value.Value))).ToArray();
            months.Add(new(month, bucket));
            foreach (var amount in bucket)
            {
                var previous = totals.GetValueOrDefault(amount.Currency) ?? new(amount.Currency, 0, 0, 0);
                totals[amount.Currency] = previous with { RentalIncome = previous.RentalIncome + amount.RentalIncome, Expenses = previous.Expenses + amount.Expenses };
            }
        }
        var expiring = leases.Where(value => value.Status == LeaseStatus.Active &&
            value.EndDate > query.EndDate && value.EndDate <= query.EndDate.AddDays(30));
        var expiryCount = await expiring.CountAsync(cancellationToken);
        var expiryList = await (from lease in expiring join unit in scopedUnits on lease.UnitId equals unit.Id
            join property in properties on unit.PropertyId equals property.Id
            orderby lease.EndDate, lease.Id
            select new DashboardLeaseResponse(lease.Id, property.Id, property.Name, unit.Number, lease.EndDate))
            .Take(10).ToListAsync(cancellationToken);
        var activity = new List<DashboardActivityResponse>();
        activity.AddRange(await payments.Where(value => value.UpdatedAt >= start && value.UpdatedAt < end)
            .OrderByDescending(value => value.UpdatedAt).ThenBy(value => value.Id).Take(10)
            .Select(value => new DashboardActivityResponse(value.Id, "payment", "Payment record", value.Status.ToString(), value.UpdatedAt)).ToListAsync(cancellationToken));
        activity.AddRange(await expenses.Where(value => value.UpdatedAt >= start && value.UpdatedAt < end)
            .OrderByDescending(value => value.UpdatedAt).ThenBy(value => value.Id).Take(10)
            .Select(value => new DashboardActivityResponse(value.Id, "expense", value.Description, value.Status.ToString(), value.UpdatedAt)).ToListAsync(cancellationToken));
        activity.AddRange(await maintenance.Where(value => value.UpdatedAt >= start && value.UpdatedAt < end)
            .OrderByDescending(value => value.UpdatedAt).ThenBy(value => value.Id).Take(10)
            .Select(value => new DashboardActivityResponse(value.Id, "maintenance", value.Title, value.Status.ToString(), value.UpdatedAt)).ToListAsync(cancellationToken));
        activity.AddRange(await leases.Where(value => value.UpdatedAt >= start && value.UpdatedAt < end)
            .OrderByDescending(value => value.UpdatedAt).ThenBy(value => value.Id).Take(10)
            .Select(value => new DashboardActivityResponse(value.Id, "lease", "Lease record", value.Status.ToString(), value.UpdatedAt)).ToListAsync(cancellationToken));
        await transaction.CommitAsync(cancellationToken);
        return new(context.OrganizationId!.Value, query.StartDate, query.EndDate, query.PropertyId, DateTimeOffset.UtcNow,
            choices, totalProperties, totalUnits, occupied, vacant, unavailable,
            totalUnits == 0 ? null : decimal.Round(occupied * 100m / totalUnits, 2),
            totals.Values.OrderBy(value => value.Currency, StringComparer.Ordinal).ToArray(), pending, expiryCount, expiryList, months,
            activity.OrderByDescending(value => value.OccurredAt).ThenBy(value => value.Kind, StringComparer.Ordinal).ThenBy(value => value.Id).Take(10).ToArray());
    }

    private sealed record Amount(string Currency, decimal Value);
}
