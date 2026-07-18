using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using NodeReel.Application.Abstractions;
using NodeReel.Infrastructure.Options;

namespace NodeReel.Infrastructure.Storage;

public sealed class MinioObjectStorage : IObjectStorage
{
    private readonly IMinioClient _client;
    private readonly MinioOptions _options;
    private readonly ILogger<MinioObjectStorage> _logger;
    private bool _bucketReady;

    public MinioObjectStorage(IMinioClient client, IOptions<MinioOptions> options, ILogger<MinioObjectStorage> logger)
    {
        _client = client;
        _options = options.Value;
        _logger = logger;
    }

    public async Task EnsureBucketAsync(CancellationToken ct = default)
    {
        if (_bucketReady) return;

        var exists = await _client.BucketExistsAsync(
            new BucketExistsArgs().WithBucket(_options.Bucket), ct);

        if (!exists)
        {
            await _client.MakeBucketAsync(new MakeBucketArgs().WithBucket(_options.Bucket), ct);
            _logger.LogInformation("Created MinIO bucket {Bucket}", _options.Bucket);
        }

        _bucketReady = true;
    }

    public async Task<string> UploadAsync(Stream content, string contentType, string? preferredKey = null, CancellationToken ct = default)
    {
        await EnsureBucketAsync(ct);
        var key = preferredKey ?? $"media/{DateTime.UtcNow:yyyy/MM/dd}/{Guid.NewGuid():N}";

        long size;
        if (content.CanSeek)
        {
            size = content.Length - content.Position;
        }
        else
        {
            var ms = new MemoryStream();
            await content.CopyToAsync(ms, ct);
            size = ms.Length;
            ms.Position = 0;
            content = ms;
        }

        await _client.PutObjectAsync(new PutObjectArgs()
            .WithBucket(_options.Bucket)
            .WithObject(key)
            .WithStreamData(content)
            .WithObjectSize(size)
            .WithContentType(contentType), ct);

        return key;
    }

    public async Task<Stream> DownloadAsync(string objectKey, CancellationToken ct = default)
    {
        await EnsureBucketAsync(ct);
        var ms = new MemoryStream();
        await _client.GetObjectAsync(new GetObjectArgs()
            .WithBucket(_options.Bucket)
            .WithObject(objectKey)
            .WithCallbackStream(stream => stream.CopyTo(ms)), ct);
        ms.Position = 0;
        return ms;
    }

    public async Task<string?> GetContentTypeAsync(string objectKey, CancellationToken ct = default)
    {
        await EnsureBucketAsync(ct);
        var stat = await _client.StatObjectAsync(
            new StatObjectArgs().WithBucket(_options.Bucket).WithObject(objectKey), ct);
        return string.IsNullOrWhiteSpace(stat.ContentType) ? null : stat.ContentType;
    }

    public async Task CopyAsync(string sourceKey, string destKey, CancellationToken ct = default)
    {
        await EnsureBucketAsync(ct);
        await _client.CopyObjectAsync(new CopyObjectArgs()
            .WithBucket(_options.Bucket)
            .WithObject(destKey)
            .WithCopyObjectSource(new CopySourceObjectArgs()
                .WithBucket(_options.Bucket)
                .WithObject(sourceKey)), ct);
    }

    public async Task<bool> ExistsAsync(string objectKey, CancellationToken ct = default)
    {
        await EnsureBucketAsync(ct);
        try
        {
            await _client.StatObjectAsync(new StatObjectArgs()
                .WithBucket(_options.Bucket)
                .WithObject(objectKey), ct);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
