using Chassis.Domain.Common;

namespace Chassis.Domain.Notes.Events;

public sealed record NoteCreated(NoteId NoteId, TenantId TenantId) : DomainEvent;
