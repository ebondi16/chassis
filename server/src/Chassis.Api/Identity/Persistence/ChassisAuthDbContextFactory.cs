using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Chassis.Api.Identity.Persistence;

/// <summary>
/// Design-time factory for <c>dotnet ef</c> (migrations, model snapshot). Kept
/// explicit so the tooling builds the context directly instead of booting the
/// full API host — which would run the Electron startup probe and the runtime
/// DB migration (see <c>Program.cs</c>).
/// </summary>
/// <remarks>
/// Mirrors the runtime registration in <see cref="IdentityComposition"/>:
/// SQLite provider + <c>UseOpenIddict()</c> so generated migrations include
/// OpenIddict's tables. The connection string is a throwaway dev default; only
/// the provider and model shape matter at design time.
/// </remarks>
internal sealed class ChassisAuthDbContextFactory : IDesignTimeDbContextFactory<ChassisAuthDbContext>
{
    public ChassisAuthDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ChassisAuthDbContext>()
            .UseSqlite(
                "Data Source=chassis.db",
                sql => sql.MigrationsHistoryTable(IdentityComposition.AuthMigrationsHistoryTable))
            .UseOpenIddict()
            .Options;

        return new ChassisAuthDbContext(options);
    }
}
