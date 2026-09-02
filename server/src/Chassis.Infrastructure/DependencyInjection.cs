using Chassis.Application.Abstractions;
using Chassis.Application.Abstractions.Persistence;
using Chassis.Domain.Common;
using Chassis.Infrastructure.Persistence;
using Chassis.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Chassis.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Assembly holding the SQLite migrations (the desktop store).</summary>
    public const string SqliteMigrationsAssembly = "Chassis.Infrastructure.Migrations.Sqlite";

    /// <summary>Assembly holding the PostgreSQL migrations (the hosted store).</summary>
    public const string NpgsqlMigrationsAssembly = "Chassis.Infrastructure.Migrations.Npgsql";

    /// <param name="provider">
    /// SQLite for the desktop build, PostgreSQL for a hosted deployment. The
    /// caller (<c>Program.cs</c>) resolves this from config, defaulting by run
    /// mode. Migrations are per-provider — see the two <c>*.Migrations.*</c>
    /// assemblies.
    /// </param>
    /// <param name="connectionString">Provider-appropriate connection string.</param>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        DatabaseProvider provider,
        string connectionString)
    {
        services.AddDbContext<ChassisDbContext>(options =>
        {
            switch (provider)
            {
                case DatabaseProvider.Sqlite:
                    options.UseSqlite(connectionString, sqlite =>
                        sqlite.MigrationsAssembly(SqliteMigrationsAssembly));
                    break;
                case DatabaseProvider.Postgres:
                    options.UseNpgsql(connectionString, npgsql =>
                        npgsql.MigrationsAssembly(NpgsqlMigrationsAssembly));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unknown database provider.");
            }
        });

        services.AddScoped<INoteRepository, NoteRepository>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();

        // Desktop default. Web swaps in a claims-based provider once auth exists (§4.2).
        services.AddScoped<ITenantProvider, LocalFixedTenantProvider>();

        return services;
    }
}
