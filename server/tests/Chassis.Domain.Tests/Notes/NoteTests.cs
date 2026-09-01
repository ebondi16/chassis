using Chassis.Domain.Common;
using Chassis.Domain.Notes;
using Chassis.Domain.Notes.Events;
using FluentAssertions;

namespace Chassis.Domain.Tests.Notes;

public class NoteTests
{
    private static readonly TenantId Tenant = TenantId.New();

    [Fact]
    public void Create_sets_tenant_and_trims_input()
    {
        var note = Note.Create(Tenant, "  Hello  ", "  world  ");

        note.TenantId.Should().Be(Tenant);
        note.Title.Should().Be("Hello");
        note.Body.Should().Be("world");
        note.Id.Value.Should().NotBe(Guid.Empty);
        note.CreatedOnUtc.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
        note.UpdatedOnUtc.Should().BeNull();
    }

    [Fact]
    public void Create_raises_NoteCreated_carrying_the_tenant()
    {
        var note = Note.Create(Tenant, "Title", "Body");

        note.DomainEvents.Should().ContainSingle().Which
            .Should().BeOfType<NoteCreated>().Which
            .TenantId.Should().Be(Tenant);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_rejects_a_blank_title(string? title)
    {
        var act = () => Note.Create(Tenant, title!, "body");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_rejects_an_overlong_title()
    {
        var act = () => Note.Create(Tenant, new string('x', Note.MaxTitleLength + 1), "body");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Edit_updates_fields_and_raises_NoteEdited()
    {
        var note = Note.Create(Tenant, "Old", "Old body");
        note.ClearDomainEvents();

        note.Edit("New", "New body");

        note.Title.Should().Be("New");
        note.Body.Should().Be("New body");
        note.UpdatedOnUtc.Should().NotBeNull();
        note.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<NoteEdited>();
    }

    [Fact]
    public void Edit_with_identical_values_is_a_no_op()
    {
        var note = Note.Create(Tenant, "Same", "Same body");
        note.ClearDomainEvents();

        note.Edit("Same", "  Same body  ");

        note.UpdatedOnUtc.Should().BeNull();
        note.DomainEvents.Should().BeEmpty();
    }
}
