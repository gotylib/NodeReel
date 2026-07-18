using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NodeReel.Application.Abstractions;

namespace NodeReel.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/providers")]
public sealed class ProvidersController : ControllerBase
{
    private readonly IProviderService _providers;

    public ProvidersController(IProviderService providers) => _providers = providers;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<NodeProviderDto>>> List(CancellationToken ct) =>
        Ok(await _providers.ListAsync(ct));

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<ActionResult<NodeProviderDto>> Create([FromBody] CreateNodeProviderDto dto, CancellationToken ct)
    {
        var created = await _providers.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(List), new { id = created.Id }, created);
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _providers.DeleteAsync(id, ct);
        return NoContent();
    }
}
