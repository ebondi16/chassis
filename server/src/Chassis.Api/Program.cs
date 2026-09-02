using System.Reflection;
using Chassis.Api;
using Chassis.Api.Endpoints;
using Chassis.Api.Identity;
using Chassis.Api.Infrastructure;
using Chassis.Api.Tenancy;
using Chassis.Application;
using Chassis.Domain.Common;
using Chassis.Infrastructure;
using Chassis.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// True when the assembly is loaded by build/design-time tooling to read metadata
// (OpenAPI document generation via GetDocument.Insider, or `dotnet ef`) rather
// than to serve requests: such a load must not spin up Electron, migrate a
// database, require real connection config, or boot the auth stack.
var isToolingLoad =
    Assembly.GetEntryAssembly()?.GetName().Name is "GetDocument.Insider"
    || EF.IsDesignTime;

var isDesktopRun = !isToolingLoad && DesktopComposition.IsDesktopRun(args);

// The web auth stack — ASP.NET Core Identity + OpenIddict + the Backend-for-
// Frontend (auth doc §5.1) — loads for a hosted web run only: never for the
// desktop build (§5.3, no login) and never for build/design-time tooling.
var isWebAuth = !isToolingLoad && !isDesktopRun;

// --- Composition root -------------------------------------------------------
// Program.cs only wires layers together. It never reaches into Domain or
// Application internals, never calls Electron.* (that lives in DesktopComposition,
// §9.4), and never touches the auth stack directly (that lives in
// Identity/IdentityComposition — auth doc §5.1). The two deployment toggles are
// these branches.
if (isDesktopRun)
{
    DesktopComposition.Enable(builder, args);
}

if (isWebAuth)
{
    IdentityComposition.AddChassisIdentity(builder);
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
        ?? (isDesktopRun ? DatabaseProvider.Sqlite : DatabaseProvider.Postgres);

var connectionString = isToolingLoad
    ? "Data Source=chassis.designtime.db"
    : builder.Configuration.GetConnectionString("Chassis")
        ?? (databaseProvider == DatabaseProvider.Sqlite
            ? "Data Source=chassis.db"
            : throw new InvalidOperationException(
                "ConnectionStrings:Chassis must be set when the database provider is Postgres."));

builder.Services.AddInfrastructure(databaseProvider, connectionString);

if (isWebAuth)
{
    // Web resolves the tenant from the authenticated user (§4.2); this scoped
    // registration replaces Infrastructure's desktop LocalFixedTenantProvider.
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ITenantProvider, ClaimsTenantProvider>();
}

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
    await scope.ServiceProvider.GetRequiredService<ChassisDbContext>().Database.MigrateAsync();

    if (isWebAuth)
    {
        // Identity + OpenIddict tables — same database, separate history table (§6).
        await scope.ServiceProvider.GetRequiredService<ChassisAuthDbContext>().Database.MigrateAsync();
    }
}

app.UseExceptionHandler();
app.UseStatusCodePages();

// Serve the built React SPA (copied into wwwroot/ from packages/ui/dist as a
// build step — see Chassis.Api.csproj). Identical call in both deployment
// modes; Electron's window just points at this same app's local URL (§5.5).
app.UseStaticFiles();

// Authentication/authorization middleware + the auth endpoints (login page,
// /connect/authorize, the BFF's /auth/* and /api/me). Web run only.
if (isWebAuth)
{
    IdentityComposition.UseChassisIdentity(app);
}

app.MapOpenApi();
if (app.Environment.IsDevelopment())
{
    app.MapScalarApiReference();
}

var notes = app.MapNoteEndpoints();
if (isWebAuth)
{
    // Desktop leaves the notes API open (§7.2 — no login); web requires a session.
    notes.RequireAuthorization();
}

// Anything not matched above (client-side routes, deep links) returns the SPA
// shell so React Router can take over.
app.MapFallbackToFile("index.html");

app.Run();

/// <summary>Exposed so an integration-test host (<c>WebApplicationFactory</c>) can target it later.</summary>
public partial class Program;
