using NodeReel.Application.Nodes;
using NodeReel.Domain.Enums;

namespace NodeReel.Application.Abstractions;

public interface IObjectStorage
{
    Task EnsureBucketAsync(CancellationToken ct = default);
    Task<string> UploadAsync(Stream content, string contentType, string? preferredKey = null, CancellationToken ct = default);
    Task<Stream> DownloadAsync(string objectKey, CancellationToken ct = default);
    Task<string?> GetContentTypeAsync(string objectKey, CancellationToken ct = default);
    Task CopyAsync(string sourceKey, string destKey, CancellationToken ct = default);
    Task<bool> ExistsAsync(string objectKey, CancellationToken ct = default);
}

public interface IVideoProcessor
{
    Task StripMetadataAsync(string inputPath, string outputPath, CancellationToken ct = default);
    Task AddInvisibleNoiseAsync(string inputPath, string outputPath, double strength, CancellationToken ct = default);
    Task TrimAsync(string inputPath, string outputPath, double startSec, double? durationSec, CancellationToken ct = default);
    Task ExtractAudioAsync(string inputPath, string outputPath, CancellationToken ct = default);
    Task RemoveAudioAsync(string inputPath, string outputPath, CancellationToken ct = default);
    Task ChangeSpeedAsync(string inputPath, string outputPath, double speed, CancellationToken ct = default);
    Task ResizeVideoAsync(string inputPath, string outputPath, int width, int height, CancellationToken ct = default);
    Task ExtractFrameAsync(string inputPath, string outputPath, double timeSec, CancellationToken ct = default);
    Task RotateVideoAsync(string inputPath, string outputPath, int degrees, CancellationToken ct = default);
    Task SetVolumeAsync(string inputPath, string outputPath, double volume, CancellationToken ct = default);
    Task ResizeImageAsync(string inputPath, string outputPath, int width, int height, CancellationToken ct = default);
    Task CropImageAsync(string inputPath, string outputPath, int x, int y, int width, int height, CancellationToken ct = default);
    Task BlurImageAsync(string inputPath, string outputPath, double sigma, CancellationToken ct = default);
    Task ImageToVideoAsync(string inputPath, string outputPath, double durationSec, int fps, CancellationToken ct = default);
    Task ConcatVideosAsync(IReadOnlyList<string> inputPaths, string outputPath, CancellationToken ct = default);

    Task TrimAudioAsync(string inputPath, string outputPath, double startSec, double? durationSec, CancellationToken ct = default);
    Task SetAudioVolumeAsync(string inputPath, string outputPath, double volume, CancellationToken ct = default);
    Task ChangeAudioSpeedAsync(string inputPath, string outputPath, double speed, CancellationToken ct = default);
    Task FadeAudioAsync(string inputPath, string outputPath, double fadeInSec, double fadeOutSec, CancellationToken ct = default);
    Task ReverseAudioAsync(string inputPath, string outputPath, CancellationToken ct = default);
    Task AudioToVideoAsync(string inputPath, string outputPath, int width, int height, CancellationToken ct = default);

    Task CropVideoAsync(string inputPath, string outputPath, int x, int y, int width, int height, CancellationToken ct = default);
    Task FlipVideoAsync(string inputPath, string outputPath, bool horizontal, CancellationToken ct = default);
    Task ReverseVideoAsync(string inputPath, string outputPath, CancellationToken ct = default);

    Task RotateImageAsync(string inputPath, string outputPath, int degrees, CancellationToken ct = default);
    Task FlipImageAsync(string inputPath, string outputPath, bool horizontal, CancellationToken ct = default);
    Task GrayscaleImageAsync(string inputPath, string outputPath, CancellationToken ct = default);
}

public interface INodeCatalog
{
    Task<IReadOnlyList<NodeDescriptor>> GetAllAsync(CancellationToken ct = default);
    Task<NodeDescriptor?> FindAsync(string providerId, string nodeId, CancellationToken ct = default);
    Task RefreshAsync(CancellationToken ct = default);
}

public interface INodeExecutor
{
    Task<NodeExecuteResponse> ExecuteAsync(string providerId, NodeExecuteRequest request, CancellationToken ct = default);
}

public interface IPipelineRunner
{
    Task<PipelineRunResultDto> StartAsync(Guid userId, PipelineRunRequestDto request, CancellationToken ct = default);
    Task<PipelineRunResultDto?> GetAsync(Guid userId, Guid runId, CancellationToken ct = default);
}

public interface IProviderService
{
    Task<IReadOnlyList<NodeProviderDto>> ListAsync(CancellationToken ct = default);
    Task<NodeProviderDto> CreateAsync(CreateNodeProviderDto dto, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface IFileService
{
    Task<FileUploadResultDto> UploadAsync(Guid userId, Stream content, string contentType, string? fileName, CancellationToken ct = default);
    Task<(Stream Stream, string ContentType, string? FileName)> DownloadAsync(Guid userId, string objectKey, CancellationToken ct = default);
}

public interface IWorkflowService
{
    Task<IReadOnlyList<WorkflowSummaryDto>> ListAsync(Guid userId, CancellationToken ct = default);
    Task<WorkflowDto?> GetAsync(Guid userId, Guid id, CancellationToken ct = default);
    Task<WorkflowDto> CreateAsync(Guid userId, SaveWorkflowDto dto, CancellationToken ct = default);
    Task<WorkflowDto?> UpdateAsync(Guid userId, Guid id, SaveWorkflowDto dto, CancellationToken ct = default);
    Task DeleteAsync(Guid userId, Guid id, CancellationToken ct = default);
}

public interface IAuthService
{
    Task<LoginResultDto?> LoginAsync(LoginRequestDto request, CancellationToken ct = default);
    Task<UserDto?> GetUserAsync(Guid userId, CancellationToken ct = default);
}

public interface IUserAdminService
{
    Task<IReadOnlyList<UserDto>> ListAsync(CancellationToken ct = default);
    Task<UserDto> CreateAsync(CreateUserDto dto, CancellationToken ct = default);
    Task ChangePasswordAsync(Guid id, ChangePasswordDto dto, CancellationToken ct = default);
    Task DeleteAsync(Guid id, Guid actingAdminId, CancellationToken ct = default);
}
