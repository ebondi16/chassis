using Chassis.Domain.Common;

namespace Chassis.Infrastructure.Tenancy;

/// <summary>
/// Desktop tenant provider: one install, one tenant, one constant id for the
/// life of the process. The id is fixed (not random per run) so local SQLite
/// rows always carry the same <see cref="TenantId"/>, which keeps a later
/// local → cloud row export a straight copy (§4.2).
/// </summary>
/// <remarks>
/// The web host replaces this with a <c>ClaimsTenantProvider</c> that resolves
/// the tenant from the authenticated user — added when web auth lands
/// (template §11 step 6 / §7.2).
/// </remarks>
public sealed class LocalFixedTenantProvider : ITenantProvider
{
    public static readonly TenantId LocalTenant =
        new(new Guid("0f1d9e2c-6a4b-4c8e-9f1a-000000000001"));

    public TenantId Current => LocalTenant;
}
