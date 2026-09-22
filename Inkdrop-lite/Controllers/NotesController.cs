using Inkdrop_lite.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Inkdrop_lite.Features.Notes;
using Inkdrop_lite.Features.Notes.Contracts;
using InkdropLite.Api.Models;

namespace Inkdrop_lite.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize(Policy = AuthorizationPolicies.ApiScope)]
public class NotesController(
    INoteService noteService,
    ILogger<NotesController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<NoteResponse>>> GetAll(
        CancellationToken cancellationToken)
    {
        var notes = await noteService.GetAllAsync(cancellationToken);
        return Ok(notes);
    }

    [HttpGet("search")]
    public async Task<ActionResult<IReadOnlyList<NoteSummaryResponse>>> Search(
        CancellationToken cancellationToken,
        [FromQuery] string? query = null,
        [FromQuery] Guid? notebookId = null,
        [FromQuery] Guid? tagId = null,
        [FromQuery] NoteStatus? status = null,
        [FromQuery] int limit = 100)
    {
        var notes = await noteService.SearchAsync(
            query, notebookId, tagId, status, limit, cancellationToken);
        return Ok(notes);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<NoteResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var note = await noteService.GetByIdAsync(id, cancellationToken);

        if (note is null)
        {
            logger.LogWarning("Note {NoteId} not found", id);
            return NotFound();
        }

        return Ok(note);
    }

    [HttpPost]
    public async Task<ActionResult<NoteResponse>> Create(
        CreateNoteRequest request,
        CancellationToken cancellationToken)
    {
        var note = await noteService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = note.Id }, note);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        UpdateNoteRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await noteService.UpdateAsync(id, request, cancellationToken);
        return updated ? NoContent() : NotFound();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        var deleted = await noteService.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}
