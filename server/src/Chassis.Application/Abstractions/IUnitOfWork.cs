namespace Chassis.Application.Abstractions;

/// <summary>
/// Commits the work accumulated by a request handler as one transaction.
/// Implemented in Infrastructure over the EF Core <c>DbContext</c>.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
