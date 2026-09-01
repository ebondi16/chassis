using Chassis.Application.Abstractions.Persistence;
using Chassis.Domain.Notes;
using Microsoft.EntityFrameworkCore;

namespace Chassis.Infrastructure.Persistence;

internal sealed class NoteRepository(ChassisDbContext db) : INoteRepository
{
    public Task<Note?> GetByIdAsync(NoteId id, CancellationToken cancellationToken = default) =>
        db.Notes.FirstOrDefaultAsync(note => note.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Note>> ListAsync(CancellationToken cancellationToken = default) =>
        await db.Notes
            .AsNoTracking()
            .OrderByDescending(note => note.CreatedOnUtc)
            .ToListAsync(cancellationToken);

    public void Add(Note note) => db.Notes.Add(note);
}
