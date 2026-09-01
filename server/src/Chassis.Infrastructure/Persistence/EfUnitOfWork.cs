using Chassis.Application.Abstractions;

namespace Chassis.Infrastructure.Persistence;

internal sealed class EfUnitOfWork(ChassisDbContext db) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        db.SaveChangesAsync(cancellationToken);
}
