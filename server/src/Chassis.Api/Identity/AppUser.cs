using System;
using Microsoft.AspNetCore.Identity;

namespace Chassis.Api.Identity;

/// <summary>
/// The identity-store row for a person who can sign in to the web build. Not a
/// domain entity — it lives in the API's auth composition area, alongside the
/// rest of the Identity/OpenIddict wiring (auth doc §2).
/// </summary>
/// <remarks>
/// One user belongs to exactly one tenant (auth doc §4.1): <see cref="TenantId"/>
/// is set once at signup and never reassigned. It is copied into a
/// <c>tenant_id</c> claim when tokens are issued, and
/// <see cref="Tenancy.ClaimsTenantProvider"/> reads it back on every API call to
/// scope the domain data (template §4.2). Guid keys line up with the domain's
/// own Guid-based ids.
/// </remarks>
public sealed class AppUser : IdentityUser<Guid>
{
    public Guid TenantId { get; set; }
}
