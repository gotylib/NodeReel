using NodeReel.Domain.Enums;

namespace NodeReel.Domain.Entities;

public class PipelineRun
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string GraphJson { get; set; } = string.Empty;
    public RunStatus Status { get; set; } = RunStatus.Pending;
    public string? Error { get; set; }
    public string? ResultObjectKey { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? FinishedAt { get; set; }

    public List<RunStep> Steps { get; set; } = [];
}
