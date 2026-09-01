using Chassis.Domain.Common;
using Chassis.Domain.Notes;
using Chassis.Infrastructure.Persistence;
using Chassis.Infrastructure.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Chassis.Infrastructure.Tests.Persistence;

/// <summary>
/// Exercises the §4.2 tenant machinery end to end against a real (in-memory)
/// SQLite database: the read-path global query filter and the write-path guard.
/// </summary>
public sealed class TenantIsolationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ChassisDbContext> _options;

    public TenantIsolationTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<ChassisDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var ctx = NewContext(TenantId.New());
        ctx.Database.EnsureCreated();
    }

    private ChassisDbContext NewContext(TenantId tenant) =>
        new(_options, new StubTenantProvider(tenant));

    [Fact]
    public async Task Reads_are_scoped_to_the_current_tenant()
    {
        var tenantA = TenantId.New();
        var tenantB = TenantId.New();

        await using (var ctx = NewContext(tenantA))
        {
            ctx.Notes.Add(Note.Create(tenantA, "A-1", string.Empty));
            ctx.Notes.Add(Note.Create(tenantA, "A-2", string.Empty));
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = NewContext(tenantB))
        {
            ctx.Notes.Add(Note.Create(tenantB, "B-1", string.Empty));
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = NewContext(tenantA))
        {
            (await ctx.Notes.Select(n => n.Title).ToListAsync())
                .Should().BeEquivalentTo("A-1", "A-2");
        }

        await using (var ctx = NewContext(tenantB))
        {
            (await ctx.Notes.CountAsync()).Should().Be(1);
        }
    }

    [Fact]
    public async Task IgnoreQueryFilters_can_still_see_every_tenant()
    {
        var tenantA = TenantId.New();
        var tenantB = TenantId.New();

        await using (var ctx = NewContext(tenantA))
        {
            ctx.Notes.Add(Note.Create(tenantA, "A", string.Empty));
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = NewContext(tenantB))
        {
            ctx.Notes.Add(Note.Create(tenantB, "B", string.Empty));
            await ctx.SaveChangesAsync();
        }

        await using var check = NewContext(tenantA);
        (await check.Notes.IgnoreQueryFilters().CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Writing_an_aggregate_from_another_tenant_is_rejected()
    {
        var current = TenantId.New();
        var other = TenantId.New();

        await using var ctx = NewContext(current);
        ctx.Notes.Add(Note.Create(other, "belongs elsewhere", string.Empty));

        var act = () => ctx.SaveChangesAsync();

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    public void Dispose() => _connection.Dispose();
}
