using Chassis.Application.Abstractions.Persistence;
using Chassis.Domain.Notes;
using MediatR;

namespace Chassis.Application.Notes.GetNoteById;

public sealed class GetNoteByIdHandler(INoteRepository notes)
    : IRequestHandler<GetNoteByIdQuery, NoteDto?>
{
    public async Task<NoteDto?> Handle(GetNoteByIdQuery request, CancellationToken cancellationToken)
    {
        var note = await notes.GetByIdAsync(new NoteId(request.Id), cancellationToken);
        return note?.ToDto();
    }
}
