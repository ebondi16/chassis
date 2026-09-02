using System.Reflection;
using Chassis.Api;
using Chassis.Api.Endpoints;
using Chassis.Api.Infrastructure;
using Chassis.Application;
using Chassis.Infrastructure;
using Chassis.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// True when the assembly is loaded by build/design-time tooling to read metadata
// (OpenAPI document generation via GetDocument.Insider, or `dotnet ef`) rather
// than to serve requests: such a load must not spin up Electron, migrate a
// database, or require real connection config.
var isToolingLoad =
    Assembly.GetEntryAssembly()?.GetName().Name is "GetDocument.Insider"
    || EF.IsDesignTime;

// --- Composition root -------------------------------------------------------
// Program.cs only wires layers together. It never reaches into Domain or
// Application internals, and never calls Electron.* — the desktop/web toggle is
// this one branch, and every ElectronNET call lives in DesktopComposition (§9.4).
var isDesktop = !isToolingLoad && DesktopComposition.IsDesktopRun(args);
if (isDesktop)
{
    DesktopComposition.Enable(builder, args);
}

builder.Services.AddApplication();

// Database provider: SQLite for the desktop build, PostgreSQL for a hosted
// deployment (§4.2). `Database:Provider` config overrides the mode default, so
// you can run the desktop build against Postgres for a test, or vice versa.
// Tooling loads never touch a database, so they use a harmless SQLite default
// rather than requiring real connection config.
var databaseProvider = isToolingLoad
    ? DatabaseProvider.Sqlite
    : builder.Configuration.GetValue<DatabaseProvider?>("Database:Provider")
        ?? (isDesktop ? DatabaseProvider.Sqlite : DatabaseProvider.Postgres);

var connectionString = isToolingLoad
    ? "Data Source=chassis.designtime.db"
    : builder.Configuration.GetConnectionString("Chassis")
        ?? (databaseProvider == DatabaseProvider.Sqlite
            ? "Data Source=chassis.db"
            : throw new InvalidOperationException(
                "ConnectionStrings:Chassis must be set when the database provider is Postgres."));

builder.Services.AddInfrastructure(databaseProvider, connectionString);

builder.Services.AddOpenApi();
builder.Services.AddExceptionHandler<ValidationExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

// Bring the database up to the latest migration on startup. Wanted for the
// desktop build (the app self-migrates its own store on upgrade); a hosted web
// deployment would instead run migrations as an explicit deploy step. EF picks
// the migrations assembly that matches the configured provider.
if (!isToolingLoad)
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<ChassisDbContext>();
    await db.Database.MigrateAsync();
}

app.UseExceptionHandler();
app.UseStatusCodePages();

// Serve the built React SPA (copied into wwwroot/ from packages/ui/dist as a
// build step — see Chassis.Api.csproj). Identical call in both deployment
// modes; Electron's window just points at this same app's local URL (§5.5).
app.UseStaticFiles();

app.MapOpenApi();
if (app.Environment.IsDevelopment())
{
    app.MapScalarApiReference();
}

app.MapNoteEndpoints();

// Anything not matched above (client-side routes, deep links) returns the SPA
// shell so React Router can take over.
app.MapFallbackToFile("index.html");

app.Run();

/// <summary>Exposed so an integration-test host (<c>WebApplicationFactory</c>) can target it later.</summary>
public partial class Program;
