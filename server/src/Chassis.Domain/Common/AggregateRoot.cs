namespace Chassis.Domain.Common;

/// <summary>
/// Base for aggregate roots: an <see cref="Entity{TId}"/> that additionally owns
/// a <see cref="TenantId"/> (set once, at creation) and a list of domain events
/// raised but not yet dispatched.
/// </summary>
public abstract class AggregateRoot<TId> : Entity<TId>, IAggregateRoot
    where TId : struct
{
    private readonly List<IDomainEvent> _domainEvents = [];

    protected AggregateRoot(TId id, TenantId tenantId) : base(id) => TenantId = tenantId;

    /// <summary>Parameterless ctor for the persistence layer's materialization only.</summary>
    protected AggregateRoot()
    {
    }

    public TenantId TenantId { get; protected set; }

    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();
}
