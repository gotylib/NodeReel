using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NodeReel.Application.Abstractions;
using NodeReel.Application.Nodes;
using NodeReel.Domain.Entities;
using NodeReel.Domain.Enums;

namespace NodeReel.Application.Services;

public sealed class PipelineRunner : IPipelineRunner
{
    private readonly INodeCatalog _catalog;
    private readonly INodeExecutor _executor;
    private readonly IPipelineRunRepository _runs;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PipelineRunner> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public PipelineRunner(
        INodeCatalog catalog,
        INodeExecutor executor,
        IPipelineRunRepository runs,
        IServiceScopeFactory scopeFactory,
        ILogger<PipelineRunner> logger)
    {
        _catalog = catalog;
        _executor = executor;
        _runs = runs;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<PipelineRunResultDto> StartAsync(Guid userId, PipelineRunRequestDto request, CancellationToken ct = default)
    {
        var run = new PipelineRun
        {
            UserId = userId,
            GraphJson = JsonSerializer.Serialize(request, JsonOptions),
            Status = RunStatus.Pending
        };

        await _runs.AddAsync(run, ct);
        var runId = run.Id;

        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var runner = scope.ServiceProvider.GetRequiredService<PipelineRunner>();
                await runner.ExecuteAsync(runId, request, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Background pipeline {RunId} crashed", runId);
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var runs = scope.ServiceProvider.GetRequiredService<IPipelineRunRepository>();
                    var failed = await runs.GetAsync(runId, CancellationToken.None);
                    if (failed is not null && failed.Status is RunStatus.Pending or RunStatus.Running)
                    {
                        failed.Status = RunStatus.Failed;
                        failed.Error = ex.Message;
                        failed.FinishedAt = DateTime.UtcNow;
                        await runs.UpdateAsync(failed, CancellationToken.None);
                    }
                }
                catch (Exception markEx)
                {
                    _logger.LogError(markEx, "Failed to mark run {RunId} as failed", runId);
                }
            }
        }, CancellationToken.None);

        return Map(run);
    }

    public async Task<PipelineRunResultDto?> GetAsync(Guid userId, Guid runId, CancellationToken ct = default)
    {
        var run = await _runs.GetForUserAsync(userId, runId, ct);
        return run is null ? null : Map(run);
    }

    public async Task ExecuteAsync(Guid runId, PipelineRunRequestDto request, CancellationToken ct)
    {
        var run = await _runs.GetAsync(runId, ct)
            ?? throw new InvalidOperationException($"Run '{runId}' not found.");

        run.Status = RunStatus.Running;
        await _runs.UpdateAsync(run, ct);

        try
        {
            var order = TopologicalSort(request.Nodes, request.Edges);
            var portValues = new Dictionary<(string nodeId, string port), string>();
            string? lastVideoKey = null;

            foreach (var node in order)
            {
                await Task.Delay(350, ct);

                var providerId = ResolveProviderId(node);
                var nodeTypeId = ResolveNodeTypeId(node);
                var step = new RunStep
                {
                    PipelineRunId = run.Id,
                    NodeInstanceId = node.Id,
                    NodeTypeId = nodeTypeId,
                    ProviderId = providerId,
                    Status = RunStatus.Running,
                    StartedAt = DateTime.UtcNow
                };
                run.Steps.Add(step);
                await _runs.UpdateAsync(run, ct);

                try
                {
                    var descriptor = await _catalog.FindAsync(step.ProviderId, step.NodeTypeId, ct)
                        ?? throw new InvalidOperationException($"Unknown node '{step.ProviderId}/{step.NodeTypeId}'.");

                    var (inputs, skipReason) = TryResolveInputs(node, descriptor, request.Edges, portValues);
                    if (skipReason is not null)
                    {
                        step.Status = RunStatus.Skipped;
                        step.Error = skipReason;
                        step.FinishedAt = DateTime.UtcNow;
                        await _runs.UpdateAsync(run, ct);
                        continue;
                    }

                    var parameters = MergeParams(node);
                    step.InputKeysJson = JsonSerializer.Serialize(inputs, JsonOptions);
                    await _runs.UpdateAsync(run, ct);

                    var response = await _executor.ExecuteAsync(step.ProviderId, new NodeExecuteRequest
                    {
                        NodeId = step.NodeTypeId,
                        Params = parameters,
                        Inputs = inputs
                    }, ct);

                    step.OutputKeysJson = JsonSerializer.Serialize(response.Outputs, JsonOptions);
                    step.Status = RunStatus.Succeeded;
                    step.FinishedAt = DateTime.UtcNow;

                    foreach (var (port, key) in response.Outputs)
                    {
                        portValues[(node.Id, port)] = key;
                        if (port is "video" or "image" or "audio" or "output" or "true" or "false" or "0" or "1" or "2" or "default")
                            lastVideoKey = key;
                    }

                    await _runs.UpdateAsync(run, ct);
                }
                catch (Exception stepEx)
                {
                    step.Status = RunStatus.Failed;
                    step.Error = stepEx.Message;
                    step.FinishedAt = DateTime.UtcNow;
                    run.Status = RunStatus.Failed;
                    run.Error = stepEx.Message;
                    run.FinishedAt = DateTime.UtcNow;
                    await _runs.UpdateAsync(run, ct);
                    return;
                }
            }

            run.Status = RunStatus.Succeeded;
            run.ResultObjectKey = lastVideoKey;
            run.FinishedAt = DateTime.UtcNow;
            await _runs.UpdateAsync(run, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Pipeline run {RunId} failed", run.Id);
            run.Status = RunStatus.Failed;
            run.Error = ex.Message;
            run.FinishedAt = DateTime.UtcNow;

            foreach (var step in run.Steps.Where(s => s.Status == RunStatus.Running))
            {
                step.Status = RunStatus.Failed;
                step.Error = ex.Message;
                step.FinishedAt = DateTime.UtcNow;
            }

            await _runs.UpdateAsync(run, ct);
        }
    }

    private static string ResolveProviderId(GraphNodeDto node)
    {
        if (!string.IsNullOrWhiteSpace(node.ProviderId) && node.ProviderId != "genericNode")
            return node.ProviderId;

        if (node.Data is not null &&
            node.Data.TryGetValue("providerId", out var el) &&
            el.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(el.GetString()))
        {
            return el.GetString()!;
        }

        return LocalNodeIds.ProviderId;
    }

    private static string ResolveNodeTypeId(GraphNodeDto node)
    {
        if (!string.IsNullOrWhiteSpace(node.Type) && node.Type != "genericNode")
            return node.Type;

        if (node.Data is not null &&
            node.Data.TryGetValue("nodeTypeId", out var el) &&
            el.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(el.GetString()))
        {
            return el.GetString()!;
        }

        return node.Type;
    }

    private static Dictionary<string, JsonElement>? MergeParams(GraphNodeDto node)
    {
        if (node.Params is not null)
            return node.Params;

        if (node.Data is null)
            return null;

        if (node.Data.TryGetValue("params", out var paramsElement) &&
            paramsElement.ValueKind == JsonValueKind.Object)
        {
            var dict = new Dictionary<string, JsonElement>();
            foreach (var prop in paramsElement.EnumerateObject())
                dict[prop.Name] = prop.Value.Clone();
            return dict;
        }

        return node.Data;
    }

    private static (Dictionary<string, string> Inputs, string? SkipReason) TryResolveInputs(
        GraphNodeDto node,
        NodeDescriptor descriptor,
        List<GraphEdgeDto> edges,
        Dictionary<(string nodeId, string port), string> portValues)
    {
        var inputs = new Dictionary<string, string>();
        var missingRequiredFromInactiveBranch = false;
        string? skipDetail = null;

        foreach (var port in descriptor.Inputs)
        {
            var edge = edges.FirstOrDefault(e =>
                e.Target == node.Id &&
                (string.IsNullOrEmpty(e.TargetHandle) || e.TargetHandle == port.Name));

            if (edge is null)
            {
                if (port.Required && descriptor.Id is not (LocalNodeIds.UploadVideo or LocalNodeIds.UploadImage or LocalNodeIds.UploadAudio or LocalNodeIds.DownloadSocialVideo or LocalNodeIds.Merge))
                    throw new InvalidOperationException($"Node '{node.Id}' missing required input '{port.Name}'.");
                continue;
            }

            var sourcePort = edge.SourceHandle;
            if (string.IsNullOrEmpty(sourcePort))
            {
                if (portValues.ContainsKey((edge.Source, port.Name)))
                    sourcePort = port.Name;
                else if (portValues.ContainsKey((edge.Source, "video")))
                    sourcePort = "video";
                else if (portValues.ContainsKey((edge.Source, "image")))
                    sourcePort = "image";
                else if (portValues.ContainsKey((edge.Source, "audio")))
                    sourcePort = "audio";
                else
                    sourcePort = descriptor.Id is LocalNodeIds.If or LocalNodeIds.Switch
                        ? edge.SourceHandle ?? "video"
                        : "video";
            }
            if (!portValues.TryGetValue((edge.Source, sourcePort), out var key))
            {
                if (!port.Required || descriptor.Id == LocalNodeIds.Merge)
                    continue;

                missingRequiredFromInactiveBranch = true;
                skipDetail = $"Waiting branch inactive: '{edge.Source}.{sourcePort}'";
                continue;
            }

            inputs[port.Name] = key;
        }

        if (descriptor.Id == LocalNodeIds.Merge)
        {
            if (inputs.Count == 0)
                return (inputs, "No upstream branch produced a video yet.");
            return (inputs, null);
        }

        if (missingRequiredFromInactiveBranch && inputs.Count == 0)
            return (inputs, skipDetail ?? "Skipped inactive branch.");

        if (missingRequiredFromInactiveBranch && descriptor.Inputs.Any(p => p.Required && !inputs.ContainsKey(p.Name)))
            return (inputs, skipDetail ?? "Skipped inactive branch.");

        foreach (var port in descriptor.Inputs.Where(p => p.Required))
        {
            if (descriptor.Id is LocalNodeIds.UploadVideo or LocalNodeIds.UploadImage or LocalNodeIds.UploadAudio or LocalNodeIds.DownloadSocialVideo)
                continue;
            if (!inputs.ContainsKey(port.Name))
                throw new InvalidOperationException($"Node '{node.Id}' missing required input '{port.Name}'.");
        }

        return (inputs, null);
    }

    private static List<GraphNodeDto> TopologicalSort(List<GraphNodeDto> nodes, List<GraphEdgeDto> edges)
    {
        var byId = nodes.ToDictionary(n => n.Id);
        var indegree = nodes.ToDictionary(n => n.Id, _ => 0);
        var adj = nodes.ToDictionary(n => n.Id, _ => new List<string>());

        foreach (var edge in edges)
        {
            if (!byId.ContainsKey(edge.Source) || !byId.ContainsKey(edge.Target))
                throw new InvalidOperationException($"Edge '{edge.Id}' references unknown node.");

            adj[edge.Source].Add(edge.Target);
            indegree[edge.Target]++;
        }

        var queue = new Queue<string>(indegree.Where(kv => kv.Value == 0).Select(kv => kv.Key));
        var result = new List<GraphNodeDto>();

        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            result.Add(byId[id]);
            foreach (var next in adj[id])
            {
                indegree[next]--;
                if (indegree[next] == 0)
                    queue.Enqueue(next);
            }
        }

        if (result.Count != nodes.Count)
            throw new InvalidOperationException("Pipeline graph contains a cycle.");

        return result;
    }

    private static PipelineRunResultDto Map(PipelineRun run) => new()
    {
        Id = run.Id,
        Status = run.Status,
        Error = run.Error,
        ResultObjectKey = run.ResultObjectKey,
        CreatedAt = run.CreatedAt,
        FinishedAt = run.FinishedAt,
        Steps = run.Steps.Select(s => new RunStepDto
        {
            NodeInstanceId = s.NodeInstanceId,
            NodeTypeId = s.NodeTypeId,
            ProviderId = s.ProviderId,
            Status = s.Status,
            Inputs = DeserializeDict(s.InputKeysJson),
            Outputs = DeserializeDict(s.OutputKeysJson),
            Error = s.Error
        }).ToList()
    };

    private static Dictionary<string, string>? DeserializeDict(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        return JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions);
    }
}
