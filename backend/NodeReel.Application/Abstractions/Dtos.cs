using System.Text.Json;
using NodeReel.Domain.Enums;

namespace NodeReel.Application.Abstractions;

public sealed class FileUploadResultDto
{
    public required string ObjectKey { get; init; }
    public required string ContentType { get; init; }
    public string? OriginalFileName { get; init; }
    public long SizeBytes { get; init; }
}

public sealed class NodeProviderDto
{
    public Guid Id { get; init; }
    public required string Name { get; init; }
    public required string BaseUrl { get; init; }
    public bool IsEnabled { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed class CreateNodeProviderDto
{
    public required string Name { get; init; }
    public required string BaseUrl { get; init; }
}

public sealed class PipelineRunRequestDto
{
    public required List<GraphNodeDto> Nodes { get; init; }
    public required List<GraphEdgeDto> Edges { get; init; }
}

public sealed class GraphNodeDto
{
    public required string Id { get; init; }
    public required string Type { get; init; }
    public string ProviderId { get; init; } = "local";
    public Dictionary<string, JsonElement>? Data { get; init; }
    public Dictionary<string, JsonElement>? Params { get; init; }
}

public sealed class GraphEdgeDto
{
    public required string Id { get; init; }
    public required string Source { get; init; }
    public required string Target { get; init; }
    public string? SourceHandle { get; init; }
    public string? TargetHandle { get; init; }
}

public sealed class PipelineRunResultDto
{
    public Guid Id { get; init; }
    public RunStatus Status { get; init; }
    public string? Error { get; init; }
    public string? ResultObjectKey { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? FinishedAt { get; init; }
    public List<RunStepDto> Steps { get; init; } = [];
}

public sealed class RunStepDto
{
    public required string NodeInstanceId { get; init; }
    public required string NodeTypeId { get; init; }
    public required string ProviderId { get; init; }
    public RunStatus Status { get; init; }
    public Dictionary<string, string>? Inputs { get; init; }
    public Dictionary<string, string>? Outputs { get; init; }
    public string? Error { get; init; }
}

public sealed class WorkflowSummaryDto
{
    public Guid Id { get; init; }
    public required string Name { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public sealed class WorkflowDto
{
    public Guid Id { get; init; }
    public required string Name { get; init; }
    public required string GraphJson { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public sealed class SaveWorkflowDto
{
    public required string Name { get; init; }
    public required string GraphJson { get; init; }
}

public sealed class LoginRequestDto
{
    public required string Username { get; init; }
    public required string Password { get; init; }
}

public sealed class LoginResultDto
{
    public required string Token { get; init; }
    public required UserDto User { get; init; }
}

public sealed class UserDto
{
    public Guid Id { get; init; }
    public required string Username { get; init; }
    public UserRole Role { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed class CreateUserDto
{
    public required string Username { get; init; }
    public required string Password { get; init; }
    public UserRole Role { get; init; } = UserRole.User;
}

public sealed class ChangePasswordDto
{
    public required string NewPassword { get; init; }
}
