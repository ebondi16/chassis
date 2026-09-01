namespace Chassis.Domain.Common;

/// <summary>
/// Marker for something that has happened in the domain that other parts of the
/// system may need to react to. Deliberately framework-free: the Domain layer
/// never references MediatR. The Application layer adapts these into notifications
/// (see <c>DomainEventNotification&lt;T&gt;</c>).
/// </summary>
public interface IDomainEvent
{
    DateTimeOffset OccurredOnUtc { get; }
}
