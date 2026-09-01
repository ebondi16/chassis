using Chassis.Domain.Common;
using FluentAssertions;

namespace Chassis.Domain.Tests.Common;

public class TenantIdTests
{
    [Fact]
    public void New_produces_distinct_values()
    {
        TenantId.New().Should().NotBe(TenantId.New());
    }

    [Fact]
    public void Parse_round_trips_through_ToString()
    {
        var original = TenantId.New();

        TenantId.Parse(original.ToString()).Should().Be(original);
    }

    [Fact]
    public void Equality_is_by_value()
    {
        var value = Guid.NewGuid();

        new TenantId(value).Should().Be(new TenantId(value));
    }
}
