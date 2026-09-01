using MediatR;

namespace Chassis.Application.Notes.GetNoteById;

public sealed record GetNoteByIdQuery(Guid Id) : IRequest<NoteDto?>;
