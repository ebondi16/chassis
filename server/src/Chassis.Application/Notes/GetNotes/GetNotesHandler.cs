using Chassis.Application.Abstractions.Persistence;
using MediatR;

namespace Chassis.Application.Notes.GetNotes;

public sealed class GetNotesHandler(INoteRepository notes)
    : IRequestHandler<GetNotesQuery, IReadOnlyList<NoteDto>>
{
    public async Task<IReadOnlyList<NoteDto>> Handle(GetNotesQuery request, CancellationToken cancellationToken)
    {
        var result = await notes.ListAsync(cancellationToken);
        return result.Select(note => note.ToDto()).ToList();
    }
}
