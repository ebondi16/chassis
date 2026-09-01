using Chassis.Application.Abstractions;
using Chassis.Application.Abstractions.Persistence;
using Chassis.Domain.Common;
using Chassis.Domain.Notes;
using MediatR;

namespace Chassis.Application.Notes.CreateNote;

public sealed class CreateNoteHandler(
    INoteRepository notes,
    IUnitOfWork unitOfWork,
    ITenantProvider tenantProvider)
    : IRequestHandler<CreateNoteCommand, NoteDto>
{
    public async Task<NoteDto> Handle(CreateNoteCommand request, CancellationToken cancellationToken)
    {
        // The tenant comes from the provider, never from the caller's payload.
        var note = Note.Create(tenantProvider.Current, request.Title, request.Body);

        notes.Add(note);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return note.ToDto();
    }
}
