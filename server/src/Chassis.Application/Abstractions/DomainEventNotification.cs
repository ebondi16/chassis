using Chassis.Domain.Common;
using MediatR;

namespace Chassis.Application.Abstractions;

/// <summary>
/// Adapts a framework-free <see cref="IDomainEvent"/> (raised in the Domain
/// layer, which knows nothing about MediatR) into a MediatR notification that
/// Application-layer handlers can subscribe to. The persistence layer wraps each
/// raised event in one of these and publishes it after a successful save.
/// </summary>
public sealed record DomainEventNotification<TDomainEvent>(TDomainEvent DomainEvent) : INotification
    where TDomainEvent : IDomainEvent;
