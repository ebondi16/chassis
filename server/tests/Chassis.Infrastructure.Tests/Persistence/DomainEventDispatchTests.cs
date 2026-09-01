using Chassis.Application.Abstractions;
using Chassis.Domain.Common;
using Chassis.Domain.Notes;
using Chassis.Domain.Notes.Events;
using Chassis.Infrastructure.Persistence;
using Chassis.Infrastructure.Tests.TestSupport;
using FluentAssertions;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Chassis.Infrastructure.Tests.Persistence;

public sealed class DomainEventDispatchTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ChassisDbContext> _options;

    public DomainEventDispatchTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<ChassisDbContext>().UseSqlite(_connection).Options;

        using var ctx = new ChassisDbContext(_options, new StubTenantProvider(TenantId.New()));
        ctx.Database.EnsureCreated();
    }

    [Fact]
    public async Task Raised_events_are_published_after_a_successful_save_and_then_cleared()
    {
        var tenant = TenantId.New();
        var publisher = new RecordingPublisher();

        await using var ctx = new ChassisDbContext(_options, new StubTenantProvider(tenant), publisher);
        var note = Note.Create(tenant, "hello", "world");
        ctx.Notes.Add(note);

        await ctx.SaveChangesAsync();

        publisher.Published.Should().ContainSingle()
            .Which.Should().BeOfType<DomainEventNotification<NoteCreated>>();
        note.DomainEvents.Should().BeEmpty();
    }

    public void Dispose() => _connection.Dispose();

    private sealed class RecordingPublisher : IPublisher
    {
        public List<object> Published { get; } = [];

        public Task Publish(object notification, CancellationToken cancellationToken = default)
        {
            Published.Add(notification);
            return Task.CompletedTask;
        }

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
        {
            Published.Add(notification!);
            return Task.CompletedTask;
        }
    }
}
