using Chassis.Domain.Common;

namespace Chassis.Domain.Notes.Events;

public sealed record NoteEdited(NoteId NoteId, TenantId TenantId) : DomainEvent;
