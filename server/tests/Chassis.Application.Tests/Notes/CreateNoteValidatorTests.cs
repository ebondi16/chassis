using Chassis.Application.Notes.CreateNote;
using Chassis.Domain.Notes;
using FluentAssertions;

namespace Chassis.Application.Tests.Notes;

public class CreateNoteValidatorTests
{
    private readonly CreateNoteValidator _validator = new();

    [Fact]
    public void Accepts_a_well_formed_command()
    {
        _validator.Validate(new CreateNoteCommand("A title", string.Empty)).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Rejects_an_empty_title()
    {
        _validator.Validate(new CreateNoteCommand(string.Empty, "body")).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Rejects_an_overlong_title()
    {
        var result = _validator.Validate(new CreateNoteCommand(new string('x', Note.MaxTitleLength + 1), "body"));

        result.IsValid.Should().BeFalse();
    }
}
