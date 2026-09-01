using Chassis.Domain.Common;
using Chassis.Domain.Notes;
using Chassis.Infrastructure.Persistence;
using Chassis.Infrastructure.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Chassis.Infrastructure.Tests.Persistence;

public sealed class NoteRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ChassisDbContext> _options;
    private readonly TenantId _tenant = TenantId.New();

    public NoteRepositoryTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<ChassisDbContext>().UseSqlite(_connection).Options;

        using var ctx = NewContext();
        ctx.Database.EnsureCreated();
    }

    private ChassisDbContext NewContext() => new(_options, new StubTenantProvider(_tenant));

    [Fact]
    public async Task ListAsync_returns_the_tenants_notes_newest_first()
    {
        await using (var ctx = NewContext())
        {
            var older = Note.Create(_tenant, "older", string.Empty);
            await Task.Delay(5);
            var newer = Note.Create(_tenant, "newer", string.Empty);
            ctx.Notes.AddRange(older, newer);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = NewContext())
        {
            var repo = new NoteRepository(ctx);

            var notes = await repo.ListAsync();

            notes.Select(n => n.Title).Should().ContainInOrder("newer", "older");
        }
    }

    [Fact]
    public async Task GetByIdAsync_round_trips_an_aggregate()
    {
        NoteId id;
        await using (var ctx = NewContext())
        {
            var note = Note.Create(_tenant, "round trip", "body text");
            id = note.Id;
            ctx.Notes.Add(note);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = NewContext())
        {
            var loaded = await new NoteRepository(ctx).GetByIdAsync(id);

            loaded.Should().NotBeNull();
            loaded!.Title.Should().Be("round trip");
            loaded.Body.Should().Be("body text");
            loaded.TenantId.Should().Be(_tenant);
        }
    }

    public void Dispose() => _connection.Dispose();
}
