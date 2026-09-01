using Chassis.Domain.Notes;

namespace Chassis.Application.Notes;

internal static class NoteMappings
{
    public static NoteDto ToDto(this Note note) =>
        new(note.Id.Value, note.Title, note.Body, note.CreatedOnUtc, note.UpdatedOnUtc);
}
