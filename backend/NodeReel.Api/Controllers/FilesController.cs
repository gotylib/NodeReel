using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NodeReel.Application.Abstractions;

namespace NodeReel.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/files")]
public sealed class FilesController : ControllerBase
{
    private readonly IFileService _files;

    public FilesController(IFileService files) => _files = files;

    [HttpPost]
    [RequestSizeLimit(2_000_000_000)]
    [RequestFormLimits(MultipartBodyLengthLimit = 2_000_000_000)]
    public async Task<ActionResult<FileUploadResultDto>> Upload(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest("File is required.");

        await using var stream = file.OpenReadStream();
        var result = await _files.UploadAsync(this.GetUserId(), stream, file.ContentType, file.FileName, ct);
        return Ok(result);
    }

    [HttpGet("by-key")]
    public async Task<IActionResult> DownloadByKey([FromQuery] string key, [FromQuery] bool download = false, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            return BadRequest("key is required.");

        return await ServeAsync(key, download, ct);
    }

    [HttpGet("{*objectKey}")]
    public async Task<IActionResult> Download(string objectKey, [FromQuery] bool download = false, CancellationToken ct = default)
    {
        objectKey = Uri.UnescapeDataString(objectKey);
        return await ServeAsync(objectKey, download, ct);
    }

    private async Task<IActionResult> ServeAsync(string objectKey, bool download, CancellationToken ct)
    {
        var (stream, contentType, fileName) = await _files.DownloadAsync(this.GetUserId(), objectKey, ct);
        // Omit download name for inline playback in <video>; ?download=1 forces attachment.
        return download
            ? File(stream, contentType, fileName ?? "download.bin", enableRangeProcessing: true)
            : File(stream, contentType, enableRangeProcessing: true);
    }
}
