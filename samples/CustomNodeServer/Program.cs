using System.Text.Json;
using System.Text.Json.Serialization;
using Minio;
using Minio.DataModel.Args;

var builder = WebApplication.CreateBuilder(args);

var endpoint = builder.Configuration["Minio:Endpoint"] ?? "127.0.0.1:9000";
var accessKey = builder.Configuration["Minio:AccessKey"] ?? "minioadmin";
var secretKey = builder.Configuration["Minio:SecretKey"] ?? "minioadmin";
var bucket = builder.Configuration["Minio:Bucket"] ?? "nodereel";
var useSsl = bool.TryParse(builder.Configuration["Minio:UseSsl"], out var ssl) && ssl;

var host = endpoint.Replace("http://", "", StringComparison.OrdinalIgnoreCase)
    .Replace("https://", "", StringComparison.OrdinalIgnoreCase);
var port = 9000;
if (host.Contains(':'))
{
    var parts = host.Split(':', 2);
    host = parts[0];
    _ = int.TryParse(parts[1], out port);
}

var handler = new HttpClientHandler { UseProxy = false };
var httpClient = new HttpClient(handler);

var minio = new MinioClient()
    .WithEndpoint(host, port)
    .WithCredentials(accessKey, secretKey)
    .WithSSL(useSsl)
    .WithHttpClient(httpClient)
    .Build();

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
app.UseCors();

var jsonOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
};

var emptySchema = JsonSerializer.SerializeToElement(new { type = "object", properties = new { } }, jsonOptions);

var descriptors = new object[]
{
    new
    {
        id = "echo-video",
        providerId = "custom",
        name = "Echo video",
        category = "sample",
        description = "Copies the input video object to a new key (federation smoke test).",
        inputs = new[] { new { name = "video", type = "video", required = true } },
        outputs = new[] { new { name = "video", type = "video", required = true } },
        paramsSchema = emptySchema
    }
};

app.MapGet("/nodes", () => Results.Json(descriptors, jsonOptions));

app.MapPost("/execute", async (HttpRequest http, CancellationToken ct) =>
{
    var request = await http.ReadFromJsonAsync<ExecuteRequest>(jsonOptions, ct);
    if (request is null)
        return Results.BadRequest(new { error = "Invalid body." });

    if (!string.Equals(request.NodeId, "echo-video", StringComparison.OrdinalIgnoreCase))
        return Results.BadRequest(new { error = $"Unknown node '{request.NodeId}'." });

    if (request.Inputs is null || !request.Inputs.TryGetValue("video", out var inputKey) || string.IsNullOrWhiteSpace(inputKey))
        return Results.BadRequest(new { error = "Missing input 'video'." });

    var exists = await minio.BucketExistsAsync(new BucketExistsArgs().WithBucket(bucket), ct);
    if (!exists)
        await minio.MakeBucketAsync(new MakeBucketArgs().WithBucket(bucket), ct);

    var outputKey = $"media/{DateTime.UtcNow:yyyy/MM/dd}/{Guid.NewGuid():N}-echo";
    await minio.CopyObjectAsync(new CopyObjectArgs()
        .WithBucket(bucket)
        .WithObject(outputKey)
        .WithCopyObjectSource(new CopySourceObjectArgs()
            .WithBucket(bucket)
            .WithObject(inputKey)), ct);

    return Results.Json(new
    {
        outputs = new Dictionary<string, string> { ["video"] = outputKey },
        logs = new[] { $"Echoed {inputKey} -> {outputKey}" }
    }, jsonOptions);
});

app.Run("http://0.0.0.0:5088");

sealed class ExecuteRequest
{
    public string NodeId { get; set; } = "";
    public Dictionary<string, JsonElement>? Params { get; set; }
    public Dictionary<string, string>? Inputs { get; set; }
}
