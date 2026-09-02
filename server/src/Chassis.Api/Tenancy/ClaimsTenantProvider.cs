using System.Security.Claims;
using Chassis.Domain.Common;

namespace Chassis.Api.Tenancy;

/// <summary>
/// Web tenant provider (template §4.2): resolves the current tenant from the
/// authenticated principal's <c>tenant_id</c> claim, which the BFF session cookie
/// carries on every API call. Lives in <c>Chassis.Api/Tenancy/</c> rather than
/// Infrastructure because it needs <see cref="IHttpContextAccessor"/> — see the
/// assembly-placement note on <see cref="ITenantProvider"/>.
/// </summary>
/// <remarks>
/// Registered in <c>Program.cs</c> for a web run only, after
/// <c>AddInfrastructure</c>, so it replaces the desktop
/// <c>LocalFixedTenantProvider</c>.
/// </remarks>
public sealed class ClaimsTenantProvider(IHttpContextAccessor httpContextAccessor) : ITenantProvider
{
    /// <summary>Claim type carrying the tenant id, issued by the authorization server.</summary>
    public const string TenantClaimType = "tenant_id";

    public TenantId Current
    {
        get
        {
            var httpContext = httpContextAccessor.HttpContext;
            if (httpContext is null)
            {
                // No request in scope (startup migration, design-time tooling).
                // There is no tenant to scope to here; any actual domain write in
                // this state trips the write-path guard in ChassisDbContext, which
                // is the behaviour we want.
                return default;
            }

            var value = httpContext.User.FindFirstValue(TenantClaimType);
            if (string.IsNullOrEmpty(value) || !Guid.TryParse(value, out var tenantId))
            {
                throw new InvalidOperationException(
                    "The authenticated principal carries no valid 'tenant_id' claim.");
            }

            return new TenantId(tenantId);
        }
    }
}
