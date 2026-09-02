using System;
using System.Security.Claims;
using Chassis.Api.Tenancy;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace Chassis.Api.Tests;

public sealed class ClaimsTenantProviderTests
{
    [Fact]
    public void Reads_the_tenant_id_claim_from_the_authenticated_principal()
    {
        var tenantId = Guid.NewGuid();
        var provider = new ClaimsTenantProvider(AccessorWith(
            new Claim(ClaimsTenantProvider.TenantClaimType, tenantId.ToString())));

        provider.Current.Value.Should().Be(tenantId);
    }

    [Fact]
    public void Throws_when_the_principal_has_no_tenant_id_claim()
    {
        var provider = new ClaimsTenantProvider(AccessorWith(new Claim("sub", "abc")));

        var act = () => provider.Current;

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Yields_the_default_tenant_outside_a_request_so_startup_migration_can_resolve_the_context()
    {
        var provider = new ClaimsTenantProvider(new HttpContextAccessor { HttpContext = null });

        provider.Current.Should().Be(default(Chassis.Domain.Common.TenantId));
    }

    private static IHttpContextAccessor AccessorWith(params Claim[] claims)
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Test")),
        };

        return new HttpContextAccessor { HttpContext = context };
    }
}
