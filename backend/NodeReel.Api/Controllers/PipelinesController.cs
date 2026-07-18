using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NodeReel.Application.Abstractions;

namespace NodeReel.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/pipelines")]
public sealed class PipelinesController : ControllerBase
{
    private readonly IPipelineRunner _runner;

    public PipelinesController(IPipelineRunner runner) => _runner = runner;

    [HttpPost("run")]
    public async Task<ActionResult<PipelineRunResultDto>> Run([FromBody] PipelineRunRequestDto request, CancellationToken ct)
    {
        if (request.Nodes is null || request.Nodes.Count == 0)
            return BadRequest(new { error = "Request failed", message = "At least one node is required." });

        var result = await _runner.StartAsync(this.GetUserId(), request, ct);
        return AcceptedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    [HttpGet("runs/{id:guid}")]
    public async Task<ActionResult<PipelineRunResultDto>> Get(Guid id, CancellationToken ct)
    {
        var result = await _runner.GetAsync(this.GetUserId(), id, ct);
        return result is null ? NotFound(new { error = "Not found", message = $"Run '{id}' not found." }) : Ok(result);
    }
}
