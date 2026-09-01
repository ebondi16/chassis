using MediatR;

namespace Chassis.Application.Notes.CreateNote;

public sealed record CreateNoteCommand(string Title, string Body) : IRequest<NoteDto>;
