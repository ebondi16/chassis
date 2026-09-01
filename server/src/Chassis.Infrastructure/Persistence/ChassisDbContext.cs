using Chassis.Application.Abstractions;
using Chassis.Domain.Common;
using Chassis.Domain.Notes;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Chassis.Infrastructure.Persistence;

/// <summary>
/// The single EF Core context. Identical schema for desktop (SQLite) and web
/// (PostgreSQL) so a local → cloud move is a row export, not a data-model
/// conversion (§4.2).
/// </summary>
public sealed class ChassisDbContext : DbContext
{
    private readonly TenantId _tenantId;
    private readonly IPublisher? _publisher;

    public ChassisDbContext(
        DbContextOptions<ChassisDbContext> options,
        ITenantProvider tenantProvider,
        IPublisher? publisher = null)
        : base(options)
    {
        _tenantId = tenantProvider.Current;
        _publisher = publisher;
    }

    public DbSet<Note> Notes => Set<Note>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // SQLite has no native date/time type and cannot ORDER BY / compare
        // DateTimeOffset. Store it as a UTC-instant-preserving long there; the
        // web provider (PostgreSQL timestamptz) keeps its native representation.
        if (Database.IsSqlite())
        {
            configurationBuilder.Properties<DateTimeOffset>()
                .HaveConversion<DateTimeOffsetToBinaryConverter>();
            configurationBuilder.Properties<DateTimeOffset?>()
                .HaveConversion<DateTimeOffsetToBinaryConverter>();
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ChassisDbContext).Assembly);

        // Structural tenant isolation on the read path (§4.2): every query against
        // an aggregate root is silently scoped to the current tenant. New
        // aggregates must add their own filter here.
        modelBuilder.Entity<Note>().HasQueryFilter(note => note.TenantId == _tenantId);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        GuardTenantOnWrite();

        var domainEvents = DrainDomainEvents();
        var affected = await base.SaveChangesAsync(cancellationToken);
        await DispatchAsync(domainEvents, cancellationToken);

        return affected;
    }

    /// <summary>
    /// Write-path counterpart to the query filter: refuse to persist an aggregate
    /// that belongs to a different tenant than the current unit of work.
    /// </summary>
    private void GuardTenantOnWrite()
    {
        foreach (var entry in ChangeTracker.Entries<IAggregateRoot>())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified))
            {
                continue;
            }

            if (entry.Entity.TenantId != _tenantId)
            {
                throw new InvalidOperationException(
                    $"{entry.Entity.GetType().Name} belongs to tenant {entry.Entity.TenantId}, " +
                    $"but this unit of work is scoped to tenant {_tenantId}.");
            }
        }
    }

    private List<IDomainEvent> DrainDomainEvents()
    {
        var roots = ChangeTracker.Entries<IAggregateRoot>()
            .Select(entry => entry.Entity)
            .Where(root => root.DomainEvents.Count > 0)
            .ToList();

        var events = roots.SelectMany(root => root.DomainEvents).ToList();

        foreach (var root in roots)
        {
            root.ClearDomainEvents();
        }

        return events;
    }

    private async Task DispatchAsync(IReadOnlyList<IDomainEvent> domainEvents, CancellationToken cancellationToken)
    {
        if (_publisher is null || domainEvents.Count == 0)
        {
            return;
        }

        foreach (var domainEvent in domainEvents)
        {
            var notificationType = typeof(DomainEventNotification<>).MakeGenericType(domainEvent.GetType());
            var notification = (INotification)Activator.CreateInstance(notificationType, domainEvent)!;
            await _publisher.Publish(notification, cancellationToken);
        }
    }
}
