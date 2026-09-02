namespace Chassis.Infrastructure.Persistence;

/// <summary>
/// Which relational store <see cref="ChassisDbContext"/> runs against. SQLite for
/// the desktop build, PostgreSQL for a hosted deployment (§4.2, §10). The schema
/// is the same shape either way; the only differences are the EF Core provider
/// and which per-provider migrations assembly is used.
/// </summary>
public enum DatabaseProvider
{
    Sqlite,
    Postgres,
}
