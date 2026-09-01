using Chassis.Application.Notes;
using Chassis.Application.Notes.CreateNote;
using Chassis.Application.Notes.GetNoteById;
using Chassis.Application.Notes.GetNotes;
using MediatR;

namespace Chassis.Api.Endpoints;

internal static class NoteEndpoints
{
    public static IEndpointRouteBuilder MapNoteEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/notes").WithTags("Notes");

        group.MapGet("/", async (ISender sender, CancellationToken cancellationToken) =>
            {
                var notes = await sender.Send(new GetNotesQuery(), cancellationToken);
                return Results.Ok(notes);
            })
            .WithName("GetNotes")
            .WithSummary("List the current tenant's notes, newest first.")
            .Produces<IReadOnlyList<NoteDto>>();

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
            {
                var note = await sender.Send(new GetNoteByIdQuery(id), cancellationToken);
                return note is null ? Results.NotFound() : Results.Ok(note);
            })
            .WithName("GetNoteById")
            .WithSummary("Fetch one note by id.")
            .Produces<NoteDto>()
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/", async (CreateNoteRequest request, ISender sender, CancellationToken cancellationToken) =>
            {
                var note = await sender.Send(new CreateNoteCommand(request.Title, request.Body), cancellationToken);
                return Results.CreatedAtRoute("GetNoteById", new { id = note.Id }, note);
            })
            .WithName("CreateNote")
            .WithSummary("Create a note for the current tenant.")
            .Produces<NoteDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        return app;
    }

    /// <summary>Request body for <c>POST /api/notes</c>; the tenant is never taken from the caller.</summary>
    internal sealed record CreateNoteRequest(string Title, string Body);
}
