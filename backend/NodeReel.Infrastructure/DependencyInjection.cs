using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minio;
using NodeReel.Application.Abstractions;
using NodeReel.Application.Services;
using NodeReel.Domain.Entities;
using NodeReel.Domain.Enums;
using NodeReel.Infrastructure.Auth;
using NodeReel.Infrastructure.Nodes;
using NodeReel.Infrastructure.Options;
using NodeReel.Infrastructure.Persistence;
using NodeReel.Infrastructure.Storage;
using NodeReel.Infrastructure.Video;

namespace NodeReel.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MinioOptions>(configuration.GetSection(MinioOptions.SectionName));
        services.Configure<FfmpegOptions>(configuration.GetSection(FfmpegOptions.SectionName));
        services.Configure<YtDlpOptions>(configuration.GetSection(YtDlpOptions.SectionName));
        services.Configure<AuthOptions>(configuration.GetSection(AuthOptions.SectionName));

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Default")));

        var minio = configuration.GetSection(MinioOptions.SectionName).Get<MinioOptions>() ?? new MinioOptions();
        services.AddSingleton<IMinioClient>(_ =>
        {
            var endpoint = minio.Endpoint.Replace("http://", "", StringComparison.OrdinalIgnoreCase)
                .Replace("https://", "", StringComparison.OrdinalIgnoreCase);
            var host = endpoint;
            var port = minio.UseSsl ? 443 : 9000;
            if (endpoint.Contains(':'))
            {
                var parts = endpoint.Split(':', 2);
                host = parts[0];
                if (!int.TryParse(parts[1], out port))
                    port = minio.UseSsl ? 443 : 9000;
            }

            var handler = new HttpClientHandler { UseProxy = false, CheckCertificateRevocationList = false };
            var httpClient = new HttpClient(handler);

            return new MinioClient()
                .WithEndpoint(host, port)
                .WithCredentials(minio.AccessKey, minio.SecretKey)
                .WithSSL(minio.UseSsl)
                .WithHttpClient(httpClient)
                .Build();
        });

        services.AddHttpClient("NodeProviders", client =>
        {
            client.Timeout = TimeSpan.FromMinutes(10);
        });

        services.AddScoped<IProviderRepository, ProviderRepository>();
        services.AddScoped<IPipelineRunRepository, PipelineRunRepository>();
        services.AddScoped<IWorkflowRepository, WorkflowRepository>();
        services.AddScoped<IMediaObjectRepository, MediaObjectRepository>();
        services.AddSingleton<IObjectStorage, MinioObjectStorage>();
        services.AddSingleton<IVideoProcessor, FfmpegVideoProcessor>();
        services.AddSingleton<ISocialVideoDownloader, YtDlpSocialDownloader>();
        services.AddSingleton<LocalNodeExecutor>();
        services.AddSingleton<INodeCatalog, AggregatingNodeCatalog>();
        services.AddScoped<INodeExecutor, CompositeNodeExecutor>();
        services.AddScoped<IFileService, FileService>();
        services.AddScoped<IProviderService, ProviderService>();
        services.AddScoped<PipelineRunner>();
        services.AddScoped<IPipelineRunner>(sp => sp.GetRequiredService<PipelineRunner>());
        services.AddScoped<IWorkflowService, WorkflowService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserAdminService, UserAdminService>();

        return services;
    }

    public static async Task InitializeInfrastructureAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync(ct);

        var auth = scope.ServiceProvider.GetRequiredService<IOptions<AuthOptions>>().Value;
        await SeedAdminAsync(db, auth, ct);
        await AssignOrphanDataToAdminAsync(db, ct);

        var storage = scope.ServiceProvider.GetRequiredService<IObjectStorage>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("InfrastructureInit");
        var minio = scope.ServiceProvider.GetRequiredService<IOptions<MinioOptions>>().Value;
        logger.LogInformation("MinIO endpoint configured as {Endpoint} (bucket {Bucket})", minio.Endpoint, minio.Bucket);

        for (var attempt = 1; attempt <= 10; attempt++)
        {
            try
            {
                await storage.EnsureBucketAsync(ct);
                break;
            }
            catch (Exception ex) when (attempt < 10)
            {
                logger.LogWarning(ex, "MinIO not ready (attempt {Attempt}/10), endpoint={Endpoint}, retrying...", attempt, minio.Endpoint);
                await Task.Delay(TimeSpan.FromSeconds(2), ct);
            }
        }

        var catalog = scope.ServiceProvider.GetRequiredService<INodeCatalog>();
        await catalog.RefreshAsync(ct);
    }

    private static async Task SeedAdminAsync(AppDbContext db, AuthOptions auth, CancellationToken ct)
    {
        var username = string.IsNullOrWhiteSpace(auth.AdminUsername) ? "admin" : auth.AdminUsername.Trim();
        var existing = await db.Users.FirstOrDefaultAsync(u => u.Username == username, ct);
        if (existing is not null) return;

        db.Users.Add(new User
        {
            Username = username,
            PasswordHash = PasswordHasher.Hash(auth.AdminPassword),
            Role = UserRole.Admin
        });
        await db.SaveChangesAsync(ct);
    }

    private static async Task AssignOrphanDataToAdminAsync(AppDbContext db, CancellationToken ct)
    {
        var admin = await db.Users.OrderBy(u => u.CreatedAt).FirstOrDefaultAsync(u => u.Role == UserRole.Admin, ct)
            ?? await db.Users.OrderBy(u => u.CreatedAt).FirstOrDefaultAsync(ct);
        if (admin is null) return;

        var empty = Guid.Empty;
        await db.Workflows.Where(w => w.UserId == empty).ExecuteUpdateAsync(s => s.SetProperty(w => w.UserId, admin.Id), ct);
        await db.PipelineRuns.Where(r => r.UserId == empty).ExecuteUpdateAsync(s => s.SetProperty(r => r.UserId, admin.Id), ct);
        await db.MediaObjects.Where(m => m.UserId == empty).ExecuteUpdateAsync(s => s.SetProperty(m => m.UserId, admin.Id), ct);
    }
}
