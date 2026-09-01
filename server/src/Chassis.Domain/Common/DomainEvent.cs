namespace Chassis.Domain.Common;

/// <summary>Convenience base for domain events; stamps the occurrence time.</summary>
public abstract record DomainEvent : IDomainEvent
{
    public DateTimeOffset OccurredOnUtc { get; init; } = DateTimeOffset.UtcNow;
}
