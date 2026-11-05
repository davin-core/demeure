using Demeure.Domain.Common;
using Demeure.Domain.Modules.Leases.Entities;
using Demeure.Domain.Modules.Organizations.Entities;
using Demeure.Domain.Modules.Payments.Entities;
using Demeure.Domain.Modules.Properties.Entities;
using Demeure.Domain.Modules.Tenants.Entities;
using Demeure.Domain.Modules.Users.Entities;
using RentalUnit = Demeure.Domain.Modules.Units.Entities.Unit;

namespace Demeure.Tests.Unit;

public sealed class DomainValidationTests
{
    [Fact]
    public void ConstructorsCreateValidDefaultsAndNormalizeValues()
    {
        var organization = new Organization(" Organization ", " MY-ORG ");
        var user = new User(" issuer ", " subject ", " User@Example.test ", " User ");
        var property = new Property(organization.Id, " Property ", " Street ", " City ", " Code ", "us");
        var unit = new RentalUnit(organization.Id, property.Id, " 101 ", 0, 0);
        var tenant = new Tenant(organization.Id, " First ", " Last ", email: " tenant@example.test ", phone: " ");
        var lease = Lease(currency: "usd");
        var payment = new Payment(organization.Id, lease.Id, 0.01m, "usd", new DateOnly(2026, 1, 1));

        Assert.Equal("my-org", organization.Slug);
        Assert.Equal("Organization", organization.Name);
        Assert.Equal("User@Example.test", user.Email);
        Assert.Equal("USER@EXAMPLE.TEST", user.NormalizedEmail);
        Assert.Equal("US", property.CountryCode);
        Assert.Equal("101", unit.Number);
        Assert.Null(tenant.UserId);
        Assert.Null(tenant.Phone);
        Assert.Equal("USD", lease.Currency);
        Assert.Equal(LeaseStatus.Draft, lease.Status);
        Assert.Equal(PaymentStatus.Pending, payment.Status);
        Assert.Null(payment.PaidAt);
        Assert.NotEqual(Guid.Empty, organization.Id);
        Assert.Equal(TimeSpan.Zero, organization.CreatedAt.Offset);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void RequiredNamesCannotBeMissing(string? value)
    {
        Assert.Throws<ArgumentException>(() => new Organization(value!, "slug"));
        Assert.Throws<ArgumentException>(() => new Organization("Organization", value!));
        Assert.Throws<ArgumentException>(() => new User("issuer", "subject", "user@example.test", value!));
        Assert.Throws<ArgumentException>(() => new Property(Guid.NewGuid(), value!, "Street", "City", "Code", "US"));
        Assert.Throws<ArgumentException>(() => new RentalUnit(Guid.NewGuid(), Guid.NewGuid(), value!));
        Assert.Throws<ArgumentException>(() => new Tenant(Guid.NewGuid(), value!, "Last"));
    }

    [Fact]
    public void EmptyIdentifiersAreRejectedAndOrganizationOwnershipCannotBeOverridden()
    {
        var id = Guid.NewGuid();
        Assert.Throws<ArgumentException>(() => new Organization("Organization", "slug", Guid.Empty));
        Assert.Throws<ArgumentException>(() => new OrganizationMembership(Guid.Empty, id));
        Assert.Throws<ArgumentException>(() => new OrganizationMembership(id, Guid.Empty));
        Assert.Throws<ArgumentException>(() => new Property(Guid.Empty, "Property", "Street", "City", "Code", "US"));
        Assert.Throws<ArgumentException>(() => new RentalUnit(id, Guid.Empty, "101"));
        Assert.Throws<ArgumentException>(() => new Tenant(id, "First", "Last", Guid.Empty));
        Assert.Throws<ArgumentException>(() => new Lease(id, Guid.Empty, id, Start, End, 1m, 0m, "USD"));
        Assert.Throws<ArgumentException>(() => new Lease(id, id, Guid.Empty, Start, End, 1m, 0m, "USD"));
        Assert.Throws<ArgumentException>(() => new Lease(id, id, id, Start, End, 1m, 0m, "USD", Guid.Empty));
        Assert.Throws<ArgumentException>(() => new Payment(id, Guid.Empty, 1m, "USD", Start));
        Assert.Throws<InvalidOperationException>(() => new Tenant(id, "First", "Last") { OrganizationId = Guid.NewGuid() });
        Assert.Throws<ArgumentOutOfRangeException>(() => new OrganizationMembership(id, id, (MembershipRole)999));
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("Display <user@example.test>")]
    [InlineData("user @example.test")]
    public void InvalidEmailsAreRejected(string email)
    {
        Assert.Throws<ArgumentException>(() => new User("issuer", "subject", email, "User"));
        Assert.Throws<ArgumentException>(() => new Tenant(Guid.NewGuid(), "First", "Last", email: email));
    }

    [Fact]
    public void LengthAndRoomLimitsAreEnforcedAtConstruction()
    {
        Assert.Throws<ArgumentException>(() => new Organization(new string('x', 201), "slug"));
        Assert.Throws<ArgumentException>(() => new User(new string('x', 301), "subject", "user@example.test", "User"));
        Assert.Throws<ArgumentException>(() => new Tenant(Guid.NewGuid(), "First", new string('x', 101)));
        Assert.Throws<ArgumentException>(() => new Tenant(Guid.NewGuid(), "First", "Last", phone: new string('x', 31)));
        Assert.Throws<ArgumentException>(() => new Property(Guid.NewGuid(), "Property", "Street", "City", "Code", "USA"));
        Assert.Throws<ArgumentException>(() => new Property(Guid.NewGuid(), "Property", "Street", "City", "Code", "1!"));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RentalUnit(Guid.NewGuid(), Guid.NewGuid(), "101", -1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RentalUnit(Guid.NewGuid(), Guid.NewGuid(), "101", 1, -1));
        Assert.Throws<ArgumentException>(() => new RentalUnit(Guid.NewGuid(), Guid.NewGuid(), new string('x', 51)));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-0.01")]
    [InlineData("1.001")]
    [InlineData("10000000000000000")]
    public void InvalidPositiveMoneyIsRejectedWithoutRounding(string input)
    {
        var amount = decimal.Parse(input, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Throws<ArgumentOutOfRangeException>(() => Lease(amount));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Payment(Guid.NewGuid(), Guid.NewGuid(), amount, "USD", Start));
    }

    [Fact]
    public void ValidMoneyBoundariesAndZeroDepositAreAccepted()
    {
        Assert.Equal(0.01m, Lease(0.01m).MonthlyRent);
        Assert.Equal(9999999999999999.99m, Lease(9999999999999999.99m).MonthlyRent);
        Assert.Equal(0m, Lease().SecurityDeposit);
        Assert.Throws<ArgumentOutOfRangeException>(() => new Lease(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Start, End, 1000m, -1m, "USD"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("US")]
    [InlineData("USDD")]
    [InlineData("U1D")]
    public void InvalidCurrencyCodesAreRejected(string currency)
    {
        Assert.Throws<ArgumentException>(() => Lease(currency: currency));
        Assert.Throws<ArgumentException>(() => new Payment(Guid.NewGuid(), Guid.NewGuid(), 1m, currency, Start));
    }

    [Fact]
    public void InvalidDatesAreRejected()
    {
        var id = Guid.NewGuid();
        Assert.Throws<ArgumentException>(() => new Lease(id, id, id, default, End, 1m, 0m, "USD"));
        Assert.Throws<ArgumentException>(() => new Lease(id, id, id, Start, default, 1m, 0m, "USD"));
        Assert.Throws<ArgumentException>(() => new Lease(id, id, id, Start, Start, 1m, 0m, "USD"));
        Assert.Throws<ArgumentException>(() => new Lease(id, id, id, End, Start, 1m, 0m, "USD"));
        Assert.Throws<ArgumentException>(() => new Payment(id, id, 1m, "USD", default));
    }

    [Fact]
    public void InvalidUpdatesAreAtomic()
    {
        var user = new User("issuer", "subject", "user@example.test", "User");
        Assert.Throws<ArgumentException>(() => user.UpdateProfile("changed@example.test", ""));
        Assert.Equal("user@example.test", user.Email);
        Assert.Equal("USER@EXAMPLE.TEST", user.NormalizedEmail);
        var property = new Property(Guid.NewGuid(), "Property", "Original street", "City", "Code", "US");
        Assert.Throws<ArgumentException>(() => property.UpdateAddress("Changed street", "City", "Code", "X"));
        Assert.Equal("Original street", property.AddressLine1);
        var unit = new RentalUnit(Guid.NewGuid(), Guid.NewGuid(), "101", 1, 1);
        Assert.Throws<ArgumentOutOfRangeException>(() => unit.UpdateDetails("102", 2, -1));
        Assert.Equal("101", unit.Number);
        Assert.Equal(1, unit.Bedrooms);
        var tenant = new Tenant(Guid.NewGuid(), "First", "Last");
        Assert.Throws<ArgumentException>(() => tenant.UpdateContact("Changed", "Last", "not-an-email"));
        Assert.Equal("First", tenant.FirstName);
        var lease = Lease();
        Assert.Throws<ArgumentException>(() => lease.UpdateTerms(Start.AddDays(1), End.AddDays(1), 2000m, 0m, "BAD!"));
        Assert.Equal(Start, lease.StartDate);
        Assert.Equal(1200m, lease.MonthlyRent);
    }

    [Fact]
    public void PaymentCompletionIsAtomicAndFullRefundRetainsReceiptEvidence()
    {
        var payment = new Payment(Guid.NewGuid(), Guid.NewGuid(), 10m, "USD", Start);
        Assert.Throws<ArgumentException>(() => payment.Complete(default));
        Assert.Throws<ArgumentException>(() => payment.Complete(DateTimeOffset.UtcNow, new string('x', 201)));
        Assert.Equal(PaymentStatus.Pending, payment.Status);
        Assert.Null(payment.PaidAt);
        Assert.Null(payment.Reference);
        var paidAt = new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.FromHours(-6));
        payment.Complete(paidAt, " receipt ");
        payment.Refund();
        Assert.Equal(PaymentStatus.Refunded, payment.Status);
        Assert.Equal(paidAt.ToUniversalTime(), payment.PaidAt);
        Assert.Equal("receipt", payment.Reference);
        Assert.Equal(10m, payment.Amount);
    }

    [Fact]
    public void TenantLoginLinkIsOptionalAndCannotBeSilentlyReplaced()
    {
        var tenant = new Tenant(Guid.NewGuid(), "First", "Last");
        Assert.Throws<ArgumentException>(() => tenant.LinkUser(Guid.Empty));
        Assert.Null(tenant.UserId);
        var userId = Guid.NewGuid();
        tenant.LinkUser(userId);
        Assert.Throws<InvalidOperationException>(() => tenant.LinkUser(Guid.NewGuid()));
        Assert.Equal(userId, tenant.UserId);
        tenant.UnlinkUser();
        Assert.Null(tenant.UserId);
        Assert.Throws<InvalidOperationException>(() => tenant.UnlinkUser());
    }

    [Fact]
    public void BusinessPropertiesAndIdentityAreNotPubliclyMutable()
    {
        Type[] types = [typeof(Organization), typeof(User), typeof(OrganizationMembership), typeof(Property),
            typeof(RentalUnit), typeof(Tenant), typeof(Lease), typeof(Payment)];
        foreach (var type in types)
        {
            Assert.All(type.GetProperties().Where(property =>
                property.Name is not (nameof(Entity.CreatedAt) or nameof(Entity.UpdatedAt) or nameof(OrganizationEntity.OrganizationId))),
                property => Assert.False(property.SetMethod?.IsPublic ?? false));
            Assert.DoesNotContain(type.GetConstructors(), constructor => constructor.GetParameters().Length == 0);
        }
    }

    private static DateOnly Start => new(2026, 1, 1);
    private static DateOnly End => new(2027, 1, 1);
    private static Lease Lease(decimal rent = 1200m, string currency = "USD") =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Start, End, rent, 0m, currency);
}
