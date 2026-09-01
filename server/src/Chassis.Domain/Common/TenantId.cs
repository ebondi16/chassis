namespace Chassis.Domain.Common;

/// <summary>
/// Identifies the tenant that owns a piece of data. Carried by every aggregate
/// root from the first commit (template §4.2), even on a desktop install that
/// will only ever have one tenant — a desktop build just uses a single fixed
/// value (see <c>LocalFixedTenantProvider</c>), while a hosted deployment
/// resolves it per request from the authenticated user.
/// </summary>
public readonly record struct TenantId(Guid Value)
{
    public static TenantId New() => new(Guid.NewGuid());

    public static TenantId Parse(string value) => new(Guid.Parse(value));

    public override string ToString() => Value.ToString();
}
