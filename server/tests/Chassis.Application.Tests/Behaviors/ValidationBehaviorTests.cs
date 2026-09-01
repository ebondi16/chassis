using Chassis.Application.Behaviors;
using Chassis.Application.Notes;
using Chassis.Application.Notes.CreateNote;
using FluentAssertions;
using FluentValidation;

namespace Chassis.Application.Tests.Behaviors;

public class ValidationBehaviorTests
{
    [Fact]
    public async Task Throws_ValidationException_when_a_validator_fails()
    {
        var behavior = new ValidationBehavior<CreateNoteCommand, NoteDto>([new CreateNoteValidator()]);

        var act = () => behavior.Handle(
            new CreateNoteCommand(string.Empty, "body"),
            () => Task.FromResult(SomeDto()),
            CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Calls_the_next_handler_when_there_are_no_validators()
    {
        var behavior = new ValidationBehavior<CreateNoteCommand, NoteDto>([]);
        var expected = SomeDto();

        var result = await behavior.Handle(
            new CreateNoteCommand("ok", "body"),
            () => Task.FromResult(expected),
            CancellationToken.None);

        result.Should().Be(expected);
    }

    private static NoteDto SomeDto() => new(Guid.NewGuid(), "t", "b", DateTimeOffset.UtcNow, null);
}
