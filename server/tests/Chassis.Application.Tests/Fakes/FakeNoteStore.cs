using Chassis.Application.Abstractions;
using Chassis.Application.Abstractions.Persistence;
using Chassis.Domain.Common;
using Chassis.Domain.Notes;

namespace Chassis.Application.Tests.Fakes;

/// <summary>In-memory stand-in that plays both the repository and the unit of work.</summary>
internal sealed class FakeNoteStore : INoteRepository, IUnitOfWork
{
    private readonly List<Note> _pending = [];

    public List<Note> Saved { get; } = [];

    public Task<Note?> GetByIdAsync(NoteId id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Saved.FirstOrDefault(note => note.Id == id));

    public Task<IReadOnlyList<Note>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Note>>(
            Saved.OrderByDescending(note => note.CreatedOnUtc).ToList());

    public void Add(Note note) => _pending.Add(note);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var count = _pending.Count;
        Saved.AddRange(_pending);
        _pending.Clear();
        return Task.FromResult(count);
    }
}

internal sealed class StubTenantProvider(TenantId tenantId) : ITenantProvider
{
    public TenantId Current { get; } = tenantId;
}
