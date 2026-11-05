using Demeure.Domain.Modules.Leases.Entities;
using Demeure.Domain.Modules.Organizations.Entities;
using Demeure.Domain.Modules.Payments.Entities;
using Demeure.Domain.Modules.Properties.Entities;
using Demeure.Domain.Modules.Tenants.Entities;
using Demeure.Domain.Modules.Units.Entities;
using Demeure.Domain.Modules.Users.Entities;
using RentalUnit = Demeure.Domain.Modules.Units.Entities.Unit;

namespace Demeure.Tests.Unit;

public sealed class DomainLifecycleTests
{
    public static IEnumerable<object[]> OrganizationTransitions => Pairs<OrganizationStatus>();
    public static IEnumerable<object[]> UserTransitions => Pairs<UserStatus>();
    public static IEnumerable<object[]> MembershipTransitions => Pairs<MembershipStatus>();
    public static IEnumerable<object[]> PropertyTransitions => Pairs<PropertyStatus>();
    public static IEnumerable<object[]> UnitTransitions => Pairs<UnitStatus>();
    public static IEnumerable<object[]> TenantTransitions => Pairs<TenantStatus>();
    public static IEnumerable<object[]> LeaseOperations => Operations<LeaseStatus>("Activate", "Cancel", "End", "Terminate");
    public static IEnumerable<object[]> PaymentOperations => Operations<PaymentStatus>("Complete", "Fail", "Retry", "Cancel", "Refund");

    [Theory, MemberData(nameof(OrganizationTransitions))]
    public void OrganizationTransitionsFollowTheLifecycle(OrganizationStatus source, OrganizationStatus target)
    {
        var entity = new Organization("Organization", "organization");
        if (source != OrganizationStatus.Active) entity.ChangeStatus(source);
        var allowed = (source, target) is
            (OrganizationStatus.Active, OrganizationStatus.Suspended or OrganizationStatus.Archived) or
            (OrganizationStatus.Suspended, OrganizationStatus.Active or OrganizationStatus.Archived);
        Check(() => entity.ChangeStatus(target), () => entity.Status, source, target, allowed);
    }

    [Theory, MemberData(nameof(UserTransitions))]
    public void UsersCanDisableAndReactivateButNotRepeatTransitions(UserStatus source, UserStatus target)
    {
        var entity = new User("issuer", "subject", "user@example.test", "User");
        if (source != UserStatus.Active) entity.ChangeStatus(source);
        Check(() => entity.ChangeStatus(target), () => entity.Status, source, target, source != target);
    }

    [Theory, MemberData(nameof(MembershipTransitions))]
    public void RemovedMembershipsCannotBeRestored(MembershipStatus source, MembershipStatus target)
    {
        var entity = new OrganizationMembership(Guid.NewGuid(), Guid.NewGuid());
        if (source != MembershipStatus.Active) entity.ChangeStatus(source);
        var allowed = (source, target) is
            (MembershipStatus.Active, MembershipStatus.Suspended or MembershipStatus.Removed) or
            (MembershipStatus.Suspended, MembershipStatus.Active or MembershipStatus.Removed);
        Check(() => entity.ChangeStatus(target), () => entity.Status, source, target, allowed);
    }

    [Theory, MemberData(nameof(PropertyTransitions))]
    public void ArchivedPropertiesCannotBeRestored(PropertyStatus source, PropertyStatus target)
    {
        var entity = Property();
        if (source != PropertyStatus.Active) entity.ChangeStatus(source);
        var allowed = (source, target) is
            (PropertyStatus.Active, PropertyStatus.Inactive or PropertyStatus.Archived) or
            (PropertyStatus.Inactive, PropertyStatus.Active or PropertyStatus.Archived);
        Check(() => entity.ChangeStatus(target), () => entity.Status, source, target, allowed);
    }

    [Theory, MemberData(nameof(UnitTransitions))]
    public void OccupiedUnitsMustBeReleasedBeforeArchiving(UnitStatus source, UnitStatus target)
    {
        var entity = new RentalUnit(Guid.NewGuid(), Guid.NewGuid(), "101");
        if (source != UnitStatus.Available) entity.ChangeStatus(source);
        var allowed = (source, target) is
            (UnitStatus.Available, UnitStatus.Occupied or UnitStatus.Unavailable or UnitStatus.Archived) or
            (UnitStatus.Occupied, UnitStatus.Available) or
            (UnitStatus.Unavailable, UnitStatus.Available or UnitStatus.Archived);
        Check(() => entity.ChangeStatus(target), () => entity.Status, source, target, allowed);
    }

    [Theory, MemberData(nameof(TenantTransitions))]
    public void ArchivedTenantsCannotBeRestored(TenantStatus source, TenantStatus target)
    {
        var entity = new Tenant(Guid.NewGuid(), "First", "Last");
        if (source != TenantStatus.Active) entity.ChangeStatus(source);
        var allowed = (source, target) is
            (TenantStatus.Active, TenantStatus.Inactive or TenantStatus.Archived) or
            (TenantStatus.Inactive, TenantStatus.Active or TenantStatus.Archived);
        Check(() => entity.ChangeStatus(target), () => entity.Status, source, target, allowed);
    }

    [Theory, MemberData(nameof(LeaseOperations))]
    public void LeaseOperationsRejectEveryInvalidSourceState(LeaseStatus source, string operation)
    {
        var lease = Lease();
        switch (source)
        {
            case LeaseStatus.Active: lease.Activate(); break;
            case LeaseStatus.Cancelled: lease.Cancel(); break;
            case LeaseStatus.Ended: lease.Activate(); lease.End(lease.EndDate); break;
            case LeaseStatus.Terminated: lease.Activate(); lease.Terminate(); break;
        }
        var (target, allowed) = operation switch
        {
            "Activate" => (LeaseStatus.Active, source == LeaseStatus.Draft),
            "Cancel" => (LeaseStatus.Cancelled, source == LeaseStatus.Draft),
            "End" => (LeaseStatus.Ended, source == LeaseStatus.Active),
            _ => (LeaseStatus.Terminated, source == LeaseStatus.Active)
        };
        Check(() =>
        {
            switch (operation)
            {
                case "Activate": lease.Activate(); break;
                case "Cancel": lease.Cancel(); break;
                case "End": lease.End(lease.EndDate); break;
                case "Terminate": lease.Terminate(); break;
            }
        }, () => lease.Status, source, target, allowed);
    }

    [Theory, MemberData(nameof(PaymentOperations))]
    public void PaymentOperationsRejectEveryInvalidSourceState(PaymentStatus source, string operation)
    {
        var payment = Payment();
        var paidAt = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.FromHours(3));
        switch (source)
        {
            case PaymentStatus.Completed: payment.Complete(paidAt, "receipt"); break;
            case PaymentStatus.Failed: payment.Fail(); break;
            case PaymentStatus.Cancelled: payment.Cancel(); break;
            case PaymentStatus.Refunded: payment.Complete(paidAt, "receipt"); payment.Refund(); break;
        }
        var (target, allowed) = operation switch
        {
            "Complete" => (PaymentStatus.Completed, source == PaymentStatus.Pending),
            "Fail" => (PaymentStatus.Failed, source == PaymentStatus.Pending),
            "Retry" => (PaymentStatus.Pending, source == PaymentStatus.Failed),
            "Cancel" => (PaymentStatus.Cancelled, source is PaymentStatus.Pending or PaymentStatus.Failed),
            _ => (PaymentStatus.Refunded, source == PaymentStatus.Completed)
        };
        var originalPaidAt = payment.PaidAt;
        var originalReference = payment.Reference;
        Check(() =>
        {
            switch (operation)
            {
                case "Complete": payment.Complete(paidAt, "receipt"); break;
                case "Fail": payment.Fail(); break;
                case "Retry": payment.Retry(); break;
                case "Cancel": payment.Cancel(); break;
                case "Refund": payment.Refund(); break;
            }
        }, () => payment.Status, source, target, allowed);
        if (allowed && operation == "Complete")
        {
            Assert.Equal(paidAt.ToUniversalTime(), payment.PaidAt);
            Assert.Equal(TimeSpan.Zero, payment.PaidAt!.Value.Offset);
        }
        else
        {
            Assert.Equal(originalPaidAt, payment.PaidAt);
            Assert.Equal(originalReference, payment.Reference);
        }
    }

    [Fact]
    public void MembershipRoleChangesRequireAnActiveMembershipAndDefinedRole()
    {
        var membership = new OrganizationMembership(Guid.NewGuid(), Guid.NewGuid());
        membership.ChangeRole(MembershipRole.Manager);
        Assert.Equal(MembershipRole.Manager, membership.Role);
        Assert.Throws<ArgumentOutOfRangeException>(() => membership.ChangeRole((MembershipRole)999));
        Assert.Equal(MembershipRole.Manager, membership.Role);
        membership.ChangeStatus(MembershipStatus.Suspended);
        Assert.Throws<InvalidOperationException>(() => membership.ChangeRole(MembershipRole.Owner));
        membership.ChangeStatus(MembershipStatus.Removed);
        Assert.Throws<InvalidOperationException>(() => membership.ChangeRole(MembershipRole.Owner));
    }

    [Fact]
    public void UndefinedStatusesNeverMutateTheEntity()
    {
        var organization = new Organization("Organization", "slug");
        var user = new User("issuer", "subject", "user@example.test", "User");
        var membership = new OrganizationMembership(Guid.NewGuid(), Guid.NewGuid());
        var property = Property();
        var unit = new RentalUnit(Guid.NewGuid(), Guid.NewGuid(), "101");
        var tenant = new Tenant(Guid.NewGuid(), "First", "Last");

        Assert.Throws<ArgumentOutOfRangeException>(() => organization.ChangeStatus((OrganizationStatus)999));
        Assert.Throws<ArgumentOutOfRangeException>(() => user.ChangeStatus((UserStatus)999));
        Assert.Throws<ArgumentOutOfRangeException>(() => membership.ChangeStatus((MembershipStatus)999));
        Assert.Throws<ArgumentOutOfRangeException>(() => property.ChangeStatus((PropertyStatus)999));
        Assert.Throws<ArgumentOutOfRangeException>(() => unit.ChangeStatus((UnitStatus)999));
        Assert.Throws<ArgumentOutOfRangeException>(() => tenant.ChangeStatus((TenantStatus)999));
        Assert.Equal(UnitStatus.Available, unit.Status);
        Assert.Equal(PropertyStatus.Active, property.Status);
    }

    [Fact]
    public void ArchivedRecordsCannotBeEdited()
    {
        var organization = new Organization("Organization", "slug");
        organization.ChangeStatus(OrganizationStatus.Archived);
        Assert.Throws<InvalidOperationException>(() => organization.Rename("Changed"));
        var property = Property();
        property.ChangeStatus(PropertyStatus.Archived);
        Assert.Throws<InvalidOperationException>(() => property.Rename("Changed"));
        Assert.Throws<InvalidOperationException>(() => property.UpdateAddress("Street", "City", "Code", "US"));
        var unit = new RentalUnit(Guid.NewGuid(), Guid.NewGuid(), "101");
        unit.ChangeStatus(UnitStatus.Archived);
        Assert.Throws<InvalidOperationException>(() => unit.UpdateDetails("102", 1, 1));
        var tenant = new Tenant(Guid.NewGuid(), "First", "Last");
        tenant.ChangeStatus(TenantStatus.Archived);
        Assert.Throws<InvalidOperationException>(() => tenant.UpdateContact("Changed", "Last"));
        Assert.Throws<InvalidOperationException>(() => tenant.LinkUser(Guid.NewGuid()));
        Assert.Throws<InvalidOperationException>(() => tenant.UnlinkUser());
    }

    [Theory]
    [InlineData(LeaseStatus.Active)]
    [InlineData(LeaseStatus.Ended)]
    [InlineData(LeaseStatus.Terminated)]
    [InlineData(LeaseStatus.Cancelled)]
    public void LeaseTermsAreImmutableOutsideDraft(LeaseStatus status)
    {
        var lease = Lease();
        if (status == LeaseStatus.Cancelled) lease.Cancel();
        else
        {
            lease.Activate();
            if (status == LeaseStatus.Ended) lease.End(lease.EndDate);
            if (status == LeaseStatus.Terminated) lease.Terminate();
        }
        Assert.Throws<InvalidOperationException>(() => lease.UpdateTerms(lease.StartDate, lease.EndDate, 2000m, 0m, "USD"));
        Assert.Equal(1200m, lease.MonthlyRent);
    }

    [Fact]
    public void LeaseCanActivateAheadOfStartButCannotEndPrematurely()
    {
        var lease = new Lease(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new DateOnly(2099, 1, 1), new DateOnly(2100, 1, 1), 1200m, 0m, "USD");
        lease.Activate();
        Assert.Throws<ArgumentException>(() => lease.End(lease.EndDate.AddDays(-1)));
        Assert.Throws<ArgumentException>(() => lease.End(default));
        Assert.Equal(LeaseStatus.Active, lease.Status);
        lease.End(lease.EndDate);
        Assert.Equal(LeaseStatus.Ended, lease.Status);
    }

    private static void Check<T>(Action action, Func<T> current, T source, T target, bool allowed)
        where T : struct, Enum
    {
        if (allowed) { action(); Assert.Equal(target, current()); }
        else { Assert.Throws<InvalidOperationException>(action); Assert.Equal(source, current()); }
    }

    private static IEnumerable<object[]> Pairs<T>() where T : struct, Enum =>
        from source in Enum.GetValues<T>() from target in Enum.GetValues<T>() select new object[] { source, target };

    private static IEnumerable<object[]> Operations<T>(params string[] operations) where T : struct, Enum =>
        from source in Enum.GetValues<T>() from operation in operations select new object[] { source, operation };

    private static Property Property() => new(Guid.NewGuid(), "Property", "Street", "City", "12345", "US");
    private static Lease Lease() => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
        new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 1), 1200m, 0m, "USD");
    private static Payment Payment() => new(Guid.NewGuid(), Guid.NewGuid(), 1200m, "USD", new DateOnly(2026, 1, 1));
}
