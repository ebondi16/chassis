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

// True when the assembly is loaded only to emit the OpenAPI document at build
// time (GetDocument.Insider): it must not migrate a database or spin up Electron.
var isDocumentGeneration = Assembly.GetEntryAssembly()?.GetName().Name is "GetDocument.Insider";

// --- Composition root -------------------------------------------------------
// Program.cs only wires layers together. It never reaches into Domain or
// Application internals, and never calls Electron.* — the desktop/web toggle is
// this one branch, and every ElectronNET call lives in DesktopComposition (§9.4).
if (!isDocumentGeneration && DesktopComposition.IsDesktopRun(args))
{
    DesktopComposition.Enable(builder, args);
}

builder.Services.AddApplication();
builder.Services.AddInfrastructure(
    builder.Configuration.GetConnectionString("Chassis") ?? "Data Source=chassis.db");

builder.Services.AddOpenApi();
builder.Services.AddExceptionHandler<ValidationExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

// Bring the local SQLite database up to the latest migration on startup. This is
// wanted for the desktop build (the app self-migrates its own db on upgrade);
// a hosted web deployment would instead run migrations as an explicit deploy step.
if (!isDocumentGeneration)
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
