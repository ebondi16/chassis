using System;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Chassis.Api.Identity;

/// <summary>
/// EF Core context for the auth stack: ASP.NET Core Identity tables plus
/// OpenIddict's application/authorization/scope/token tables.
/// </summary>
/// <remarks>
/// Deliberately separate from <c>ChassisDbContext</c> (the domain context in
/// Chassis.Infrastructure) so none of this auth wiring leaks outside the API's
/// composition area. It still targets the <em>same physical database and schema</em>
/// as the domain data (auth doc §6) — same connection string — and just keeps its
/// own migrations-history table (<c>__EFMigrationsHistoryAuth</c>, set in
/// <see cref="IdentityComposition"/>) so the two histories coexist in one file.
/// OpenIddict's entities are added by <c>options.UseOpenIddict()</c> at
/// registration time.
/// </remarks>
public sealed class ChassisAuthDbContext(DbContextOptions<ChassisAuthDbContext> options)
    : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>(options);
