namespace Chassis.Domain.Common;

/// <summary>
/// Non-generic marker so the persistence layer can find aggregate roots in the
/// change tracker without knowing their id type — used to enforce tenant scoping
/// on write and to collect domain events after a successful save.
/// </summary>
public interface IAggregateRoot
{
    TenantId TenantId { get; }

    IReadOnlyList<IDomainEvent> DomainEvents { get; }

    void ClearDomainEvents();
}
