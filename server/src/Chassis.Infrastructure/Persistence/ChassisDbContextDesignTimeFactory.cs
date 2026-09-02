using Chassis.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Chassis.Infrastructure.Persistence;

/// <summary>
/// Lets <c>dotnet ef</c> build a <see cref="ChassisDbContext"/> without the web
/// host. Which provider (and therefore which migrations assembly) it targets is
/// chosen by the <c>ChassisMigrationsProvider</c> environment variable —
/// <c>Sqlite</c> (default) or <c>Postgres</c>:
///
/// <code>
/// ChassisMigrationsProvider=Sqlite  dotnet ef migrations add &lt;Name&gt; \
///   --project src/Chassis.Infrastructure.Migrations.Sqlite \
///   --startup-project src/Chassis.Infrastructure -o Migrations
///
/// ChassisMigrationsProvider=Postgres dotnet ef migrations add &lt;Name&gt; \
///   --project src/Chassis.Infrastructure.Migrations.Npgsql \
///   --startup-project src/Chassis.Infrastructure -o Migrations
/// </code>
///
/// The connection strings here are design-time placeholders — no database is
/// contacted to add or script a migration.
/// </summary>
public sealed class ChassisDbContextDesignTimeFactory : IDesignTimeDbContextFactory<ChassisDbContext>
{
    public ChassisDbContext CreateDbContext(string[] args)
    {
        var provider = Enum.TryParse<DatabaseProvider>(
            Environment.GetEnvironmentVariable("ChassisMigrationsProvider"), ignoreCase: true, out var parsed)
            ? parsed
            : DatabaseProvider.Sqlite;

        var options = new DbContextOptionsBuilder<ChassisDbContext>();

        switch (provider)
        {
            case DatabaseProvider.Sqlite:
                options.UseSqlite("Data Source=chassis.design.db", sqlite =>
                    sqlite.MigrationsAssembly(DependencyInjection.SqliteMigrationsAssembly));
                break;
            case DatabaseProvider.Postgres:
                options.UseNpgsql("Host=localhost;Database=chassis;Username=chassis;Password=chassis", npgsql =>
                    npgsql.MigrationsAssembly(DependencyInjection.NpgsqlMigrationsAssembly));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unknown database provider.");
        }

        return new ChassisDbContext(options.Options, new LocalFixedTenantProvider());
    }
}
