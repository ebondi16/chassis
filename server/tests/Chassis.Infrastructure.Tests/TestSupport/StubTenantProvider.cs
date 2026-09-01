using Chassis.Domain.Common;

namespace Chassis.Infrastructure.Tests.TestSupport;

internal sealed class StubTenantProvider(TenantId tenantId) : ITenantProvider
{
    public TenantId Current { get; } = tenantId;
}
