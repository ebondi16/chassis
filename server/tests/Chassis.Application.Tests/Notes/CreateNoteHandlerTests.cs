using Chassis.Application.Notes.CreateNote;
using Chassis.Application.Tests.Fakes;
using Chassis.Domain.Common;
using FluentAssertions;

namespace Chassis.Application.Tests.Notes;

public class CreateNoteHandlerTests
{
    [Fact]
    public async Task Handle_persists_a_note_scoped_to_the_provider_tenant()
    {
        var tenant = TenantId.New();
        var store = new FakeNoteStore();
        var handler = new CreateNoteHandler(store, store, new StubTenantProvider(tenant));

        var dto = await handler.Handle(new CreateNoteCommand("Shopping list", "eggs, milk"), CancellationToken.None);

        dto.Title.Should().Be("Shopping list");
        dto.Id.Should().NotBe(Guid.Empty);
        store.Saved.Should().ContainSingle();
        store.Saved[0].TenantId.Should().Be(tenant);
    }

    [Fact]
    public async Task Handle_ignores_any_caller_supplied_tenant_and_uses_the_provider()
    {
        var providerTenant = TenantId.New();
        var store = new FakeNoteStore();
        var handler = new CreateNoteHandler(store, store, new StubTenantProvider(providerTenant));

        await handler.Handle(new CreateNoteCommand("t", "b"), CancellationToken.None);

        store.Saved[0].TenantId.Should().Be(providerTenant);
    }
}
