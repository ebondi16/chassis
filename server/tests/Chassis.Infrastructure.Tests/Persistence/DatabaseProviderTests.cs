using Chassis.Domain.Notes;
using Chassis.Infrastructure;
using Chassis.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Chassis.Infrastructure.Tests.Persistence;

public sealed class DatabaseProviderTests
{
    [Theory]
    [InlineData(DatabaseProvider.Sqlite, "Microsoft.EntityFrameworkCore.Sqlite", "Data Source=:memory:")]
    [InlineData(DatabaseProvider.Postgres, "Npgsql.EntityFrameworkCore.PostgreSQL", "Host=localhost;Database=chassis;Username=u;Password=p")]
    public void AddInfrastructure_wires_the_requested_provider_and_builds_the_model(
        DatabaseProvider provider, string expectedProviderName, string connectionString)
    {
        var services = new ServiceCollection();
        services.AddInfrastructure(provider, connectionString);

        using var scope = services.BuildServiceProvider().CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChassisDbContext>();

        db.Database.ProviderName.Should().Be(expectedProviderName);

        // Touching the model runs OnModelCreating + ConfigureConventions without
        // contacting a database — proves the mapping, the value converters, and
        // the tenant query filter all compile under this provider.
        var note = db.Model.FindEntityType(typeof(Note));
        note.Should().NotBeNull();
        note!.GetProperties().Select(p => p.Name).Should().Contain(nameof(Note.TenantId));
        note.GetDeclaredQueryFilters().Should().NotBeEmpty("every aggregate is tenant-scoped (§4.2)");
    }
}
