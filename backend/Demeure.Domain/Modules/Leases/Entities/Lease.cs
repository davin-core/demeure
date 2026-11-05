using System.Diagnostics.CodeAnalysis;
using Demeure.Domain.Common;

namespace Demeure.Domain.Modules.Leases.Entities;

public sealed class Lease : OrganizationEntity
{
    private Lease() { }

    [SetsRequiredMembers]
    public Lease(Guid organizationId, Guid unitId, Guid tenantId, DateOnly startDate, DateOnly endDate,
        decimal monthlyRent, decimal securityDeposit, string currency, Guid? rentalApplicationId = null)
    {
        OrganizationId = DomainGuard.Id(organizationId, nameof(organizationId));
        UnitId = DomainGuard.Id(unitId, nameof(unitId));
        TenantId = DomainGuard.Id(tenantId, nameof(tenantId));
        RentalApplicationId = DomainGuard.OptionalId(rentalApplicationId, nameof(rentalApplicationId));
        UpdateTerms(startDate, endDate, monthlyRent, securityDeposit, currency);
    }

    public Guid UnitId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid? RentalApplicationId { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public decimal MonthlyRent { get; private set; }
    public decimal SecurityDeposit { get; private set; }
    public string Currency { get; private set; } = "";
    public LeaseStatus Status { get; private set; } = LeaseStatus.Draft;

    public void UpdateTerms(DateOnly startDate, DateOnly endDate, decimal monthlyRent, decimal securityDeposit, string currency)
    {
        RequireStatus(LeaseStatus.Draft);
        DomainGuard.Date(startDate, nameof(startDate));
        DomainGuard.Date(endDate, nameof(endDate));
        if (endDate <= startDate) throw new ArgumentException("The end date must follow the start date.", nameof(endDate));
        var rent = DomainGuard.Money(monthlyRent, nameof(monthlyRent));
        var deposit = DomainGuard.Money(securityDeposit, nameof(securityDeposit), allowZero: true);
        var validatedCurrency = DomainGuard.Currency(currency);
        StartDate = startDate;
        EndDate = endDate;
        MonthlyRent = rent;
        SecurityDeposit = deposit;
        Currency = validatedCurrency;
    }

    public void Activate()
    {
        RequireStatus(LeaseStatus.Draft);
        Status = LeaseStatus.Active;
    }

    public void Cancel()
    {
        RequireStatus(LeaseStatus.Draft);
        Status = LeaseStatus.Cancelled;
    }

    public void End(DateOnly effectiveDate)
    {
        RequireStatus(LeaseStatus.Active);
        DomainGuard.Date(effectiveDate, nameof(effectiveDate));
        if (effectiveDate < EndDate) throw new ArgumentException("A lease cannot end before its scheduled end date.", nameof(effectiveDate));
        Status = LeaseStatus.Ended;
    }

    public void Terminate()
    {
        RequireStatus(LeaseStatus.Active);
        Status = LeaseStatus.Terminated;
    }

    private void RequireStatus(LeaseStatus expected)
    {
        if (Status != expected) throw new InvalidOperationException($"The lease must be {expected} for this operation.");
    }
}

public enum LeaseStatus { Draft, Active, Ended, Terminated, Cancelled }
