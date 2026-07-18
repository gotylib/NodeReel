using Microsoft.EntityFrameworkCore;
using NodeReel.Application.Services;
using NodeReel.Domain.Entities;

namespace NodeReel.Infrastructure.Persistence;

public sealed class ProviderRepository : IProviderRepository
{
    private readonly AppDbContext _db;

    public ProviderRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<NodeProvider>> ListAsync(CancellationToken ct = default) =>
        await _db.NodeProviders.OrderBy(x => x.CreatedAt).ToListAsync(ct);

    public async Task<IReadOnlyList<NodeProvider>> ListEnabledAsync(CancellationToken ct = default) =>
        await _db.NodeProviders.Where(x => x.IsEnabled).OrderBy(x => x.CreatedAt).ToListAsync(ct);

    public Task<NodeProvider?> GetAsync(Guid id, CancellationToken ct = default) =>
        _db.NodeProviders.FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task AddAsync(NodeProvider provider, CancellationToken ct = default)
    {
        _db.NodeProviders.Add(provider);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _db.NodeProviders.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) return;
        _db.NodeProviders.Remove(entity);
        await _db.SaveChangesAsync(ct);
    }
}

public sealed class PipelineRunRepository : IPipelineRunRepository
{
    private readonly AppDbContext _db;

    public PipelineRunRepository(AppDbContext db) => _db = db;

    public async Task AddAsync(PipelineRun run, CancellationToken ct = default)
    {
        _db.PipelineRuns.Add(run);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(PipelineRun run, CancellationToken ct = default)
    {
        // Ensure new steps are Added (not Modified). Do not call Update(run) — it marks the graph Modified.
        foreach (var step in run.Steps)
        {
            var entry = _db.Entry(step);
            if (entry.State == EntityState.Detached)
                _db.RunSteps.Add(step);
        }

        await _db.SaveChangesAsync(ct);
    }

    public Task<PipelineRun?> GetAsync(Guid id, CancellationToken ct = default) =>
        _db.PipelineRuns.Include(x => x.Steps).FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<PipelineRun?> GetForUserAsync(Guid userId, Guid id, CancellationToken ct = default) =>
        _db.PipelineRuns.Include(x => x.Steps).FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);
}

public sealed class WorkflowRepository : IWorkflowRepository
{
    private readonly AppDbContext _db;

    public WorkflowRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<Workflow>> ListByUserAsync(Guid userId, CancellationToken ct = default) =>
        await _db.Workflows.Where(x => x.UserId == userId).OrderByDescending(x => x.UpdatedAt).ToListAsync(ct);

    public Task<Workflow?> GetForUserAsync(Guid userId, Guid id, CancellationToken ct = default) =>
        _db.Workflows.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);

    public async Task AddAsync(Workflow workflow, CancellationToken ct = default)
    {
        _db.Workflows.Add(workflow);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Workflow workflow, CancellationToken ct = default)
    {
        var tracked = await _db.Workflows.FirstOrDefaultAsync(x => x.Id == workflow.Id && x.UserId == workflow.UserId, ct);
        if (tracked is null) return;

        tracked.Name = workflow.Name;
        tracked.GraphJson = workflow.GraphJson;
        tracked.UpdatedAt = workflow.UpdatedAt;
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid userId, Guid id, CancellationToken ct = default)
    {
        var entity = await _db.Workflows.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);
        if (entity is null) return;
        _db.Workflows.Remove(entity);
        await _db.SaveChangesAsync(ct);
    }
}

public sealed class MediaObjectRepository : IMediaObjectRepository
{
    private readonly AppDbContext _db;

    public MediaObjectRepository(AppDbContext db) => _db = db;

    public async Task AddAsync(MediaObject media, CancellationToken ct = default)
    {
        _db.MediaObjects.Add(media);
        await _db.SaveChangesAsync(ct);
    }

    public Task<MediaObject?> GetByKeyAsync(string objectKey, CancellationToken ct = default) =>
        _db.MediaObjects.FirstOrDefaultAsync(x => x.ObjectKey == objectKey, ct);
}
