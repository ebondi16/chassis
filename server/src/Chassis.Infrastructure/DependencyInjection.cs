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
    /// <param name="connectionString">
    /// SQLite connection string for the desktop build (e.g. <c>Data Source=chassis.db</c>).
    /// The web host passes a PostgreSQL connection string and swaps the provider.
    /// </param>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<ChassisDbContext>(options => options.UseSqlite(connectionString));

        services.AddScoped<INoteRepository, NoteRepository>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();

        // Desktop default. Web swaps in a claims-based provider once auth exists (§4.2).
        services.AddScoped<ITenantProvider, LocalFixedTenantProvider>();

        return services;
    }
}
