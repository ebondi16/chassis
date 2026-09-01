namespace Chassis.Domain.Common;

/// <summary>
/// Supplies the tenant the current unit of work belongs to. Lives in Domain
/// (template v1.1 §4.2) because tenant scoping is a domain concern; the
/// implementations live outside it:
/// <list type="bullet">
///   <item><c>LocalFixedTenantProvider</c> — Infrastructure, a plain constant (desktop);</item>
///   <item><c>ClaimsTenantProvider</c> — <c>Chassis.Api/Tenancy/</c>, resolves from the
///   authenticated user and needs <c>IHttpContextAccessor</c>, which Infrastructure
///   must stay free of (§4.1).</item>
/// </list>
/// </summary>
public interface ITenantProvider
{
    TenantId Current { get; }
}
