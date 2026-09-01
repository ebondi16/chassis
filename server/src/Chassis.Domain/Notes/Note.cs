using Chassis.Domain.Common;
using Chassis.Domain.Notes.Events;

namespace Chassis.Domain.Notes;

/// <summary>
/// Placeholder aggregate. Its job in the template is to demonstrate the shape
/// every aggregate follows:
/// <list type="bullet">
///   <item>a strongly-typed id (<see cref="NoteId"/>);</item>
///   <item>a <see cref="TenantId"/> fixed at creation and never reassigned;</item>
///   <item>state mutated only through intention-revealing methods, never setters;</item>
///   <item>domain events raised for things worth reacting to.</item>
/// </list>
/// Replace it with real aggregates in template §11 step 6.
/// </summary>
public sealed class Note : AggregateRoot<NoteId>
{
    public const int MaxTitleLength = 200;

    private Note(NoteId id, TenantId tenantId, string title, string body)
        : base(id, tenantId)
    {
        Title = title;
        Body = body;
        CreatedOnUtc = DateTimeOffset.UtcNow;
    }

    /// <summary>For the persistence layer's materialization only.</summary>
    private Note()
    {
    }

    public string Title { get; private set; } = string.Empty;

    public string Body { get; private set; } = string.Empty;

    public DateTimeOffset CreatedOnUtc { get; private set; }

    public DateTimeOffset? UpdatedOnUtc { get; private set; }

    public static Note Create(TenantId tenantId, string title, string body)
    {
        var note = new Note(NoteId.New(), tenantId, NormalizeTitle(title), NormalizeBody(body));
        note.Raise(new NoteCreated(note.Id, tenantId));
        return note;
    }

    public void Edit(string title, string body)
    {
        var normalizedTitle = NormalizeTitle(title);
        var normalizedBody = NormalizeBody(body);

        if (normalizedTitle == Title && normalizedBody == Body)
        {
            return;
        }

        Title = normalizedTitle;
        Body = normalizedBody;
        UpdatedOnUtc = DateTimeOffset.UtcNow;
        Raise(new NoteEdited(Id, TenantId));
    }

    private static string NormalizeTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("A note must have a title.", nameof(title));
        }

        title = title.Trim();

        return title.Length <= MaxTitleLength
            ? title
            : throw new ArgumentException($"A note title cannot exceed {MaxTitleLength} characters.", nameof(title));
    }

    private static string NormalizeBody(string body) => (body ?? string.Empty).Trim();
}
