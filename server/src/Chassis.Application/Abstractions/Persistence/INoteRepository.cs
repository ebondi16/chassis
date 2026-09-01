using Chassis.Domain.Notes;

namespace Chassis.Application.Abstractions.Persistence;

/// <summary>
/// Collection-like access to the <see cref="Note"/> aggregate. Implementations
/// are always tenant-scoped: reads only ever return the current tenant's rows
/// (enforced structurally by the DbContext query filter, §4.2).
/// </summary>
public interface INoteRepository
{
    Task<Note?> GetByIdAsync(NoteId id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Note>> ListAsync(CancellationToken cancellationToken = default);

    void Add(Note note);
}
