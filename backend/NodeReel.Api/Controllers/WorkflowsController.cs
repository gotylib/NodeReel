using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NodeReel.Application.Abstractions;

namespace NodeReel.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/workflows")]
public sealed class WorkflowsController : ControllerBase
{
    private readonly IWorkflowService _workflows;

    public WorkflowsController(IWorkflowService workflows) => _workflows = workflows;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<WorkflowSummaryDto>>> List(CancellationToken ct) =>
        Ok(await _workflows.ListAsync(this.GetUserId(), ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<WorkflowDto>> Get(Guid id, CancellationToken ct)
    {
        var workflow = await _workflows.GetAsync(this.GetUserId(), id, ct);
        return workflow is null ? NotFound() : Ok(workflow);
    }

    [HttpPost]
    public async Task<ActionResult<WorkflowDto>> Create([FromBody] SaveWorkflowDto dto, CancellationToken ct)
    {
        var created = await _workflows.CreateAsync(this.GetUserId(), dto, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<WorkflowDto>> Update(Guid id, [FromBody] SaveWorkflowDto dto, CancellationToken ct)
    {
        var updated = await _workflows.UpdateAsync(this.GetUserId(), id, dto, ct);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _workflows.DeleteAsync(this.GetUserId(), id, ct);
        return NoContent();
    }
}
