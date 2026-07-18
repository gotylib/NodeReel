using NodeReel.Domain.Enums;

namespace NodeReel.Domain.Entities;

public class RunStep
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PipelineRunId { get; set; }
    public PipelineRun PipelineRun { get; set; } = null!;
    public string NodeInstanceId { get; set; } = string.Empty;
    public string NodeTypeId { get; set; } = string.Empty;
    public string ProviderId { get; set; } = "local";
    public RunStatus Status { get; set; } = RunStatus.Pending;
    public string? InputKeysJson { get; set; }
    public string? OutputKeysJson { get; set; }
    public string? Error { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
}
