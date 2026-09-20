using Inkdrop_lite.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Inkdrop_lite.Features.Tags;
using Inkdrop_lite.Features.Tags.Contracts;

namespace Inkdrop_lite.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize(Policy = AuthorizationPolicies.ApiScope)]
public class TagsController(
    ITagService tagService,
    ILogger<TagsController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TagResponse>>> GetAll(
        CancellationToken cancellationToken)
    {
        var tags = await tagService.GetAllAsync(cancellationToken);
        return Ok(tags);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TagResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var tag = await tagService.GetByIdAsync(id, cancellationToken);

        if (tag is null)
        {
            logger.LogWarning("Tag {TagId} not found", id);
            return NotFound();
        }

        return Ok(tag);
    }

    [HttpPost]
    public async Task<ActionResult<TagResponse>> Create(
        CreateTagRequest request,
        CancellationToken cancellationToken)
    {
        var tag = await tagService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = tag.Id }, tag);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        UpdateTagRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await tagService.UpdateAsync(id, request, cancellationToken);
        return updated ? NoContent() : NotFound();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        var deleted = await tagService.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}
