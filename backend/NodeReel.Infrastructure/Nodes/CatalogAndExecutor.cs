using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NodeReel.Application.Abstractions;
using NodeReel.Application.Nodes;
using NodeReel.Application.Services;

namespace NodeReel.Infrastructure.Nodes;

public sealed class CompositeNodeExecutor : INodeExecutor
{
    private readonly LocalNodeExecutor _local;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IProviderRepository _providers;
    private readonly ILogger<CompositeNodeExecutor> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public CompositeNodeExecutor(
        LocalNodeExecutor local,
        IHttpClientFactory httpClientFactory,
        IProviderRepository providers,
        ILogger<CompositeNodeExecutor> logger)
    {
        _local = local;
        _httpClientFactory = httpClientFactory;
        _providers = providers;
        _logger = logger;
    }

    public async Task<NodeExecuteResponse> ExecuteAsync(string providerId, NodeExecuteRequest request, CancellationToken ct = default)
    {
        if (providerId == LocalNodeIds.ProviderId || string.IsNullOrWhiteSpace(providerId))
            return await _local.ExecuteAsync(request, ct);

        var providers = await _providers.ListEnabledAsync(ct);
        var provider = providers.FirstOrDefault(p => p.Id.ToString("N") == providerId || p.Id.ToString() == providerId)
            ?? throw new InvalidOperationException($"Provider '{providerId}' not found or disabled.");

        var client = _httpClientFactory.CreateClient("NodeProviders");
        var url = $"{provider.BaseUrl.TrimEnd('/')}/execute";

        _logger.LogInformation("Proxy execute {NodeId} to {Url}", request.NodeId, url);

        using var response = await client.PostAsJsonAsync(url, request, JsonOptions, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"Remote provider '{provider.Name}' failed: {(int)response.StatusCode} {body}");
        }

        var result = await response.Content.ReadFromJsonAsync<NodeExecuteResponse>(JsonOptions, ct)
            ?? throw new InvalidOperationException("Remote provider returned empty execute response.");

        return result;
    }
}

public sealed class AggregatingNodeCatalog : INodeCatalog
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AggregatingNodeCatalog> _logger;
    private readonly object _gate = new();
    private List<NodeDescriptor> _cache = LocalNodeExecutor.Descriptors.ToList();
    private DateTime _lastRefresh = DateTime.MinValue;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public AggregatingNodeCatalog(
        IServiceScopeFactory scopeFactory,
        IHttpClientFactory httpClientFactory,
        ILogger<AggregatingNodeCatalog> logger)
    {
        _scopeFactory = scopeFactory;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<IReadOnlyList<NodeDescriptor>> GetAllAsync(CancellationToken ct = default)
    {
        if (DateTime.UtcNow - _lastRefresh > TimeSpan.FromSeconds(30))
            await RefreshAsync(ct);

        lock (_gate)
            return _cache.ToList();
    }

    public async Task<NodeDescriptor?> FindAsync(string providerId, string nodeId, CancellationToken ct = default)
    {
        var all = await GetAllAsync(ct);
        return all.FirstOrDefault(n =>
            n.ProviderId.Equals(providerId, StringComparison.OrdinalIgnoreCase) &&
            n.Id.Equals(nodeId, StringComparison.OrdinalIgnoreCase));
    }

    public async Task RefreshAsync(CancellationToken ct = default)
    {
        var aggregated = new List<NodeDescriptor>(LocalNodeExecutor.Descriptors);

        using var scope = _scopeFactory.CreateScope();
        var providersRepo = scope.ServiceProvider.GetRequiredService<IProviderRepository>();
        var providers = await providersRepo.ListEnabledAsync(ct);
        var client = _httpClientFactory.CreateClient("NodeProviders");

        foreach (var provider in providers)
        {
            try
            {
                var url = $"{provider.BaseUrl.TrimEnd('/')}/nodes";
                var remote = await client.GetFromJsonAsync<List<NodeDescriptor>>(url, JsonOptions, ct) ?? [];
                var providerKey = provider.Id.ToString("N");

                foreach (var node in remote)
                {
                    aggregated.Add(new NodeDescriptor
                    {
                        Id = node.Id,
                        ProviderId = providerKey,
                        Name = node.Name,
                        Category = node.Category,
                        Description = node.Description,
                        Icon = node.Icon,
                        Subtitle = node.Subtitle ?? node.Category,
                        Inputs = node.Inputs,
                        Outputs = node.Outputs,
                        ParamsSchema = node.ParamsSchema.ValueKind == JsonValueKind.Undefined
                            ? NodeSchemaHelper.EmptyObjectSchema()
                            : node.ParamsSchema
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch nodes from provider {Name} ({Url})", provider.Name, provider.BaseUrl);
            }
        }

        lock (_gate)
        {
            _cache = aggregated;
            _lastRefresh = DateTime.UtcNow;
        }
    }
}
