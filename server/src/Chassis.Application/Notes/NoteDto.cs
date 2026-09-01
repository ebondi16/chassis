namespace Chassis.Application.Notes;

/// <summary>Read model returned across the API boundary. No domain types leak out.</summary>
public sealed record NoteDto(
    Guid Id,
    string Title,
    string Body,
    DateTimeOffset CreatedOnUtc,
    DateTimeOffset? UpdatedOnUtc);
