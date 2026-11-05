using Demeure.Domain.Modules.Properties.Entities;

namespace Demeure.Tests.Unit;

public sealed class PropertyManagerAssignmentTests
{
    [Fact]
    public void AssignmentRetainsValidatedOrganizationPropertyAndUser()
    {
        var organizationId = Guid.NewGuid();
        var propertyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var assignment = new PropertyManagerAssignment(organizationId, propertyId, userId);
        Assert.Equal(organizationId, assignment.OrganizationId);
        Assert.Equal(propertyId, assignment.PropertyId);
        Assert.Equal(userId, assignment.UserId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void AssignmentRejectsEmptyRelationshipIdentifiers(int missing)
    {
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        ids[missing] = Guid.Empty;
        Assert.Throws<ArgumentException>(() => new PropertyManagerAssignment(ids[0], ids[1], ids[2]));
    }
}
