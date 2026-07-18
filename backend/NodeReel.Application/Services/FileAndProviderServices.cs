using System.Text.Json;
using NodeReel.Application.Abstractions;
using NodeReel.Application.Nodes;
using NodeReel.Domain.Entities;

namespace NodeReel.Application.Services;

public sealed class FileService : IFileService
{
    private readonly IObjectStorage _storage;
    private readonly IMediaObjectRepository _media;

    public FileService(IObjectStorage storage, IMediaObjectRepository media)
    {
        _storage = storage;
        _media = media;
    }

    public async Task<FileUploadResultDto> UploadAsync(Guid userId, Stream content, string contentType, string? fileName, CancellationToken ct = default)
    {
        await using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        var size = buffer.Length;
        buffer.Position = 0;

        var preferredKey = $"users/{userId:N}/media/{DateTime.UtcNow:yyyy/MM/dd}/{Guid.NewGuid():N}";
        var key = await _storage.UploadAsync(buffer, contentType, preferredKey, ct);

        await _media.AddAsync(new MediaObject
        {
            UserId = userId,
            ObjectKey = key,
            ContentType = contentType,
            OriginalFileName = fileName,
            SizeBytes = size
        }, ct);

        return new FileUploadResultDto
        {
            ObjectKey = key,
            ContentType = contentType,
            OriginalFileName = fileName,
            SizeBytes = size
        };
    }

    public async Task<(Stream Stream, string ContentType, string? FileName)> DownloadAsync(Guid userId, string objectKey, CancellationToken ct = default)
    {
        var media = await _media.GetByKeyAsync(objectKey, ct);
        if (media is not null && media.UserId != userId)
            throw new UnauthorizedAccessException("You do not have access to this file.");

        if (media is null)
        {
            var prefix = $"users/{userId:N}/";
            if (objectKey.StartsWith("users/", StringComparison.OrdinalIgnoreCase) &&
                !objectKey.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException("You do not have access to this file.");
        }

        var contentType = media?.ContentType
            ?? await _storage.GetContentTypeAsync(objectKey, ct)
            ?? GuessContentType(objectKey);
        var stream = await _storage.DownloadAsync(objectKey, ct);
        var fileName = media?.OriginalFileName ?? Path.GetFileName(objectKey);
        if (string.IsNullOrWhiteSpace(fileName) || !fileName.Contains('.'))
        {
            fileName = contentType switch
            {
                var c when c.StartsWith("video/", StringComparison.OrdinalIgnoreCase) => "output.mp4",
                var c when c.StartsWith("audio/", StringComparison.OrdinalIgnoreCase) => "output.mp3",
                var c when c.StartsWith("image/", StringComparison.OrdinalIgnoreCase) => "output.png",
                _ => "download.bin"
            };
        }
        return (stream, contentType, fileName);
    }

    private static string GuessContentType(string objectKey)
    {
        var ext = Path.GetExtension(objectKey).ToLowerInvariant();
        return ext switch
        {
            ".mp4" => "video/mp4",
            ".webm" => "video/webm",
            ".mov" => "video/quicktime",
            ".mkv" => "video/x-matroska",
            ".mp3" => "audio/mpeg",
            ".wav" => "audio/wav",
            ".aac" => "audio/aac",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };
    }
}

public sealed class ProviderService : IProviderService
{
    private readonly IProviderRepository _repository;
    private readonly INodeCatalog _catalog;

    public ProviderService(IProviderRepository repository, INodeCatalog catalog)
    {
        _repository = repository;
        _catalog = catalog;
    }

    public async Task<IReadOnlyList<NodeProviderDto>> ListAsync(CancellationToken ct = default)
    {
        var items = await _repository.ListAsync(ct);
        return items.Select(Map).ToList();
    }

    public async Task<NodeProviderDto> CreateAsync(CreateNodeProviderDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Name) || string.IsNullOrWhiteSpace(dto.BaseUrl))
            throw new ArgumentException("Name and BaseUrl are required.");

        var entity = new NodeProvider
        {
            Name = dto.Name.Trim(),
            BaseUrl = dto.BaseUrl.Trim().TrimEnd('/'),
            IsEnabled = true
        };

        await _repository.AddAsync(entity, ct);
        await _catalog.RefreshAsync(ct);
        return Map(entity);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await _repository.DeleteAsync(id, ct);
        await _catalog.RefreshAsync(ct);
    }

    private static NodeProviderDto Map(NodeProvider p) => new()
    {
        Id = p.Id,
        Name = p.Name,
        BaseUrl = p.BaseUrl,
        IsEnabled = p.IsEnabled,
        CreatedAt = p.CreatedAt
    };
}

public interface IProviderRepository
{
    Task<IReadOnlyList<NodeProvider>> ListAsync(CancellationToken ct = default);
    Task<IReadOnlyList<NodeProvider>> ListEnabledAsync(CancellationToken ct = default);
    Task<NodeProvider?> GetAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(NodeProvider provider, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface IPipelineRunRepository
{
    Task AddAsync(PipelineRun run, CancellationToken ct = default);
    Task UpdateAsync(PipelineRun run, CancellationToken ct = default);
    Task<PipelineRun?> GetAsync(Guid id, CancellationToken ct = default);
    Task<PipelineRun?> GetForUserAsync(Guid userId, Guid id, CancellationToken ct = default);
}

public interface IWorkflowRepository
{
    Task<IReadOnlyList<Workflow>> ListByUserAsync(Guid userId, CancellationToken ct = default);
    Task<Workflow?> GetForUserAsync(Guid userId, Guid id, CancellationToken ct = default);
    Task AddAsync(Workflow workflow, CancellationToken ct = default);
    Task UpdateAsync(Workflow workflow, CancellationToken ct = default);
    Task DeleteAsync(Guid userId, Guid id, CancellationToken ct = default);
}

public interface IMediaObjectRepository
{
    Task AddAsync(MediaObject media, CancellationToken ct = default);
    Task<MediaObject?> GetByKeyAsync(string objectKey, CancellationToken ct = default);
}

public sealed class WorkflowService : IWorkflowService
{
    private readonly IWorkflowRepository _repository;

    public WorkflowService(IWorkflowRepository repository) => _repository = repository;

    public async Task<IReadOnlyList<WorkflowSummaryDto>> ListAsync(Guid userId, CancellationToken ct = default)
    {
        var items = await _repository.ListByUserAsync(userId, ct);
        return items.Select(w => new WorkflowSummaryDto
        {
            Id = w.Id,
            Name = w.Name,
            CreatedAt = w.CreatedAt,
            UpdatedAt = w.UpdatedAt
        }).ToList();
    }

    public async Task<WorkflowDto?> GetAsync(Guid userId, Guid id, CancellationToken ct = default)
    {
        var w = await _repository.GetForUserAsync(userId, id, ct);
        return w is null ? null : Map(w);
    }

    public async Task<WorkflowDto> CreateAsync(Guid userId, SaveWorkflowDto dto, CancellationToken ct = default)
    {
        var name = string.IsNullOrWhiteSpace(dto.Name) ? "Untitled pipeline" : dto.Name.Trim();
        var entity = new Workflow
        {
            UserId = userId,
            Name = name,
            GraphJson = string.IsNullOrWhiteSpace(dto.GraphJson) ? """{"nodes":[],"edges":[]}""" : dto.GraphJson
        };
        await _repository.AddAsync(entity, ct);
        return Map(entity);
    }

    public async Task<WorkflowDto?> UpdateAsync(Guid userId, Guid id, SaveWorkflowDto dto, CancellationToken ct = default)
    {
        var entity = await _repository.GetForUserAsync(userId, id, ct);
        if (entity is null) return null;

        entity.Name = string.IsNullOrWhiteSpace(dto.Name) ? entity.Name : dto.Name.Trim();
        entity.GraphJson = string.IsNullOrWhiteSpace(dto.GraphJson) ? entity.GraphJson : dto.GraphJson;
        entity.UpdatedAt = DateTime.UtcNow;
        await _repository.UpdateAsync(entity, ct);
        return Map(entity);
    }

    public Task DeleteAsync(Guid userId, Guid id, CancellationToken ct = default) =>
        _repository.DeleteAsync(userId, id, ct);

    private static WorkflowDto Map(Workflow w) => new()
    {
        Id = w.Id,
        Name = w.Name,
        GraphJson = w.GraphJson,
        CreatedAt = w.CreatedAt,
        UpdatedAt = w.UpdatedAt
    };
}
