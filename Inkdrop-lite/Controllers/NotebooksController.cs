using Inkdrop_lite.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Inkdrop_lite.Features.Notebooks;
using Inkdrop_lite.Features.Notebooks.Contracts;

namespace Inkdrop_lite.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize(Policy = AuthorizationPolicies.ApiScope)]
public class NotebooksController(
    INotebookService notebookService,
    ILogger<NotebooksController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<NotebookResponse>>> GetAll(
        CancellationToken cancellationToken)
    {
        var notebooks = await notebookService.GetAllAsync(cancellationToken);
        return Ok(notebooks);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<NotebookResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var notebook = await notebookService.GetByIdAsync(id, cancellationToken);

        if (notebook is null)
        {
            logger.LogWarning("Notebook {NotebookId} not found", id);
            return NotFound();
        }

        return Ok(notebook);
    }

    [HttpPost]
    public async Task<ActionResult<NotebookResponse>> Create(
        CreateNotebookRequest request,
        CancellationToken cancellationToken)
    {
        var notebook = await notebookService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = notebook.Id }, notebook);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        UpdateNotebookRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await notebookService.UpdateAsync(id, request, cancellationToken);
        return updated ? NoContent() : NotFound();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        var deleted = await notebookService.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}
