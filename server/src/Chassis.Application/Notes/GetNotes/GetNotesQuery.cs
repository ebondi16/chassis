using MediatR;

namespace Chassis.Application.Notes.GetNotes;

public sealed record GetNotesQuery : IRequest<IReadOnlyList<NoteDto>>;
