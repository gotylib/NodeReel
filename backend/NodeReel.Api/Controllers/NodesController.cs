using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NodeReel.Application.Abstractions;
using NodeReel.Application.Nodes;

namespace NodeReel.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/nodes")]
public sealed class NodesController : ControllerBase
{
    private readonly INodeCatalog _catalog;

    public NodesController(INodeCatalog catalog) => _catalog = catalog;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<NodeDescriptor>>> GetAll(CancellationToken ct)
    {
        var nodes = await _catalog.GetAllAsync(ct);
        return Ok(nodes);
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(CancellationToken ct)
    {
        await _catalog.RefreshAsync(ct);
        return NoContent();
    }
}
