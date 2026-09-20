using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Inkdrop_lite.Features.Common;
using Inkdrop_lite.Features.Notebooks;
using Inkdrop_lite.Features.Notebooks.Contracts;
using Inkdrop_lite.Features.Notes;
using Inkdrop_lite.Features.Notes.Contracts;
using Inkdrop_lite.Features.Tags;
using Inkdrop_lite.Features.Tags.Contracts;
using InkdropLite.Api.Models;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Inkdrop_lite.Features.Mcp;

/// <summary>
/// MCP tools exposed at /mcp. They call the same feature services as the controllers, so the
/// caller's Entra identity and the per-user isolation in AppDbContext apply unchanged.
/// Deliberately no delete tools: deletes are hard deletes and cannot be undone.
/// </summary>
[McpServerToolType]
public sealed class InkdropTools
{
    [McpServerTool(Name = "list_notebooks", ReadOnly = true)]
    [Description("List all notebooks with their parent (ParentNotebookId) so the hierarchy can be rebuilt.")]
    public static Task<IReadOnlyList<NotebookResponse>> ListNotebooks(
        INotebookService notebooks,
        CancellationToken cancellationToken) =>
        notebooks.GetAllAsync(cancellationToken);

    [McpServerTool(Name = "list_tags", ReadOnly = true)]
    [Description("List all tags.")]
    public static Task<IReadOnlyList<TagResponse>> ListTags(
        ITagService tags,
        CancellationToken cancellationToken) =>
        tags.GetAllAsync(cancellationToken);

    [McpServerTool(Name = "search_notes", ReadOnly = true)]
    [Description(
        "Search notes, most recently updated first. The query matches note title, content, " +
        "notebook name and tag name (case-insensitive). Returns summaries without content; " +
        "use get_note to read the Markdown.")]
    public static Task<IReadOnlyList<NoteSummaryResponse>> SearchNotes(
        INoteService notes,
        CancellationToken cancellationToken,
        [Description("Text to look for. Omit to list recent notes.")] string? query = null,
        [Description("Only notes in this notebook.")] Guid? notebookId = null,
        [Description("Only notes with this tag.")] Guid? tagId = null,
        [Description("Only notes with this status: None, Active, OnHold, Completed, Dropped.")]
        NoteStatus? status = null,
        [Description("Maximum results, 1-100.")] int limit = 25) =>
        notes.SearchAsync(query, notebookId, tagId, status, limit, cancellationToken);

    [McpServerTool(Name = "get_note", ReadOnly = true)]
    [Description(
        "Read a note's Markdown. Short notes come back whole. Long notes are returned in pages " +
        "(default 12000 characters) together with a heading outline; check hasMore/nextOffset and " +
        "prefer reading only the section you need via 'section' instead of paging through everything. " +
        "search_notes reports contentLength so you can judge the cost before reading.")]
    public static async Task<NoteContentResponse> GetNote(
        INoteService notes,
        [Description("The note id.")] Guid id,
        CancellationToken cancellationToken,
        [Description("Read only the section under this heading (case-insensitive; a unique substring is enough).")]
        string? section = null,
        [Description("Character offset to start from, relative to the section when one is given.")]
        int offset = 0,
        [Description("Maximum characters to return, 1000-100000.")]
        int maxChars = NoteContentReader.DefaultPageChars)
    {
        var note = await notes.GetByIdAsync(id, cancellationToken)
            ?? throw new McpException($"Note {id} was not found.");

        try
        {
            return NoteContentResponse.From(
                note,
                NoteContentReader.Read(note.Content, section, offset, maxChars));
        }
        catch (ArgumentException exception)
        {
            throw new McpException(exception.Message);
        }
    }

    [McpServerTool(Name = "create_note", Destructive = false)]
    [Description("Create a Markdown note. Every note must belong to an existing notebook (see list_notebooks).")]
    public static Task<NoteResponse> CreateNote(
        INoteService notes,
        CancellationToken cancellationToken,
        [Description("Note title, max 256 characters.")] string title,
        [Description("Notebook the note belongs to.")] Guid notebookId,
        [Description("Markdown content, max 1 MiB.")] string content = "",
        [Description("None, Active, OnHold, Completed or Dropped.")] NoteStatus status = NoteStatus.None,
        [Description("Ids of existing tags (see list_tags).")] Guid[]? tagIds = null,
        [Description("Pin the note.")] bool pinned = false,
        [Description("Id of the note this one was created from, if it is based on a template note.")]
        Guid? sourceTemplateId = null) =>
        ExecuteAsync(() => notes.CreateAsync(
            Validated(new CreateNoteRequest(
                title, content, status, notebookId, tagIds, pinned, sourceTemplateId)),
            cancellationToken));

    [McpServerTool(Name = "update_note", Destructive = false, Idempotent = true)]
    [Description(
        "Update a note. Only the fields you pass change; omitted fields keep their value. " +
        "Passing tagIds replaces the note's whole tag set.")]
    public static async Task<NoteResponse> UpdateNote(
        INoteService notes,
        CancellationToken cancellationToken,
        [Description("The note id.")] Guid id,
        [Description("New title, max 256 characters.")] string? title = null,
        [Description("New Markdown content (replaces the existing content), max 1 MiB.")]
        string? content = null,
        [Description("None, Active, OnHold, Completed or Dropped.")] NoteStatus? status = null,
        [Description("Pin or unpin.")] bool? pinned = null,
        [Description("Move the note to this notebook.")] Guid? notebookId = null,
        [Description("Replace the note's tags with exactly these tag ids. Pass [] to clear them.")]
        Guid[]? tagIds = null)
    {
        var current = await notes.GetByIdAsync(id, cancellationToken)
            ?? throw new McpException($"Note {id} was not found.");

        var request = Validated(new UpdateNoteRequest(
            title ?? current.Title,
            content ?? current.Content,
            status ?? current.Status,
            notebookId ?? current.NotebookId,
            tagIds ?? current.TagIds.ToArray(),
            pinned ?? current.Pinned));

        await ExecuteAsync(() => notes.UpdateAsync(id, request, cancellationToken));

        return await notes.GetByIdAsync(id, cancellationToken)
            ?? throw new McpException($"Note {id} was not found.");
    }

    // Controllers get model validation for free; tools do not, so run the same DataAnnotations here.
    private static T Validated<T>(T request) where T : notnull
    {
        var results = new List<ValidationResult>();

        if (!Validator.TryValidateObject(request, new ValidationContext(request), results, true))
        {
            throw new McpException(string.Join(
                " ",
                results.Select(result => result.ErrorMessage)));
        }

        return request;
    }

    // Business-rule failures are safe to show to the agent; anything else stays generic.
    private static async Task<TResult> ExecuteAsync<TResult>(Func<Task<TResult>> action)
    {
        try
        {
            return await action();
        }
        catch (RuleViolationException exception)
        {
            throw new McpException(exception.Message);
        }
    }
}
