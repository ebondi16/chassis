using Chassis.Domain.Notes;
using FluentValidation;

namespace Chassis.Application.Notes.CreateNote;

public sealed class CreateNoteValidator : AbstractValidator<CreateNoteCommand>
{
    public CreateNoteValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty()
            .MaximumLength(Note.MaxTitleLength);

        RuleFor(x => x.Body)
            .NotNull();
    }
}
