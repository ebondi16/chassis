using Chassis.Application.Abstractions;
using Chassis.Domain.Notes.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Chassis.Application.Notes.EventHandlers;

/// <summary>
/// Example domain-event subscriber. Exists to prove the raise → save → dispatch
/// path is wired end to end; replace with real reactions (projections, outbox,
/// integration events) as features land.
/// </summary>
internal sealed class NoteCreatedLogger(ILogger<NoteCreatedLogger> logger)
    : INotificationHandler<DomainEventNotification<NoteCreated>>
{
    public Task Handle(DomainEventNotification<NoteCreated> notification, CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;
        logger.LogInformation(
            "Note {NoteId} created for tenant {TenantId} at {OccurredOnUtc:o}",
            domainEvent.NoteId,
            domainEvent.TenantId,
            domainEvent.OccurredOnUtc);

        return Task.CompletedTask;
    }
}
