namespace Demeure.Domain.Common;

public abstract class Entity
{
    protected Entity() { }

    protected Entity(Guid id) => Id = DomainGuard.Id(id, nameof(id));

    public Guid Id { get; private set; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
