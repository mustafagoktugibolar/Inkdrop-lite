using Inkdrop_lite.Authorization;
using Inkdrop_lite.Features.Attachments;
using Inkdrop_lite.Features.Attachments.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inkdrop_lite.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize(Policy = AuthorizationPolicies.ApiScope)]
public class AttachmentsController(
    IAttachmentService attachmentService,
    AttachmentStorageOptions options) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AttachmentResponse>>> GetAll(
        CancellationToken cancellationToken) =>
        Ok(await attachmentService.GetAllAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AttachmentResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var attachment = await attachmentService.GetByIdAsync(id, cancellationToken);
        return attachment is null ? NotFound() : Ok(attachment);
    }

    /// <summary>
    /// Always served as a download with sniffing and active content disabled, so an uploaded
    /// HTML or SVG file can never run script in this API's origin.
    /// </summary>
    [HttpGet("{id:guid}/content")]
    public async Task<IActionResult> GetContent(Guid id, CancellationToken cancellationToken)
    {
        var content = await attachmentService.OpenContentAsync(id, cancellationToken);

        if (content is null)
        {
            return NotFound();
        }

        Response.Headers.XContentTypeOptions = "nosniff";
        Response.Headers.ContentSecurityPolicy = "sandbox; default-src 'none'";
        Response.Headers.CacheControl = "private, no-store";

        return File(content.Content, content.Attachment.ContentType, content.Attachment.Name);
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<AttachmentResponse>> Upload(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file.Length > options.MaxBytes)
        {
            return Problem(
                detail: $"The file is larger than the {options.MaxBytes} byte limit.",
                statusCode: StatusCodes.Status413PayloadTooLarge);
        }

        await using var stream = file.OpenReadStream();
        var attachment = await attachmentService.UploadAsync(
            stream,
            file.FileName,
            file.ContentType,
            cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = attachment.Id }, attachment);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await attachmentService.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}
