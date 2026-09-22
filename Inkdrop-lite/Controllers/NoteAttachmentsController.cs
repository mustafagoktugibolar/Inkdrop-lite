using Inkdrop_lite.Authorization;
using Inkdrop_lite.Features.Attachments;
using Inkdrop_lite.Features.Attachments.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inkdrop_lite.Controllers;

[Route("api/notes/{noteId:guid}/attachments")]
[ApiController]
[Authorize(Policy = AuthorizationPolicies.ApiScope)]
public class NoteAttachmentsController(IAttachmentService attachmentService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AttachmentResponse>>> GetForNote(
        Guid noteId,
        CancellationToken cancellationToken)
    {
        var attachments = await attachmentService.GetForNoteAsync(noteId, cancellationToken);
        return attachments is null ? NotFound() : Ok(attachments);
    }

    [HttpPut("{attachmentId:guid}")]
    public async Task<IActionResult> Attach(
        Guid noteId,
        Guid attachmentId,
        CancellationToken cancellationToken)
    {
        var found = await attachmentService.AttachToNoteAsync(noteId, attachmentId, cancellationToken);
        return found ? NoContent() : NotFound();
    }

    [HttpDelete("{attachmentId:guid}")]
    public async Task<IActionResult> Detach(
        Guid noteId,
        Guid attachmentId,
        CancellationToken cancellationToken)
    {
        var found = await attachmentService.DetachFromNoteAsync(noteId, attachmentId, cancellationToken);
        return found ? NoContent() : NotFound();
    }
}
