namespace NodeReel.Infrastructure.Options;

public sealed class MinioOptions
{
    public const string SectionName = "Minio";
    public string Endpoint { get; set; } = "localhost:9000";
    public string AccessKey { get; set; } = "minioadmin";
    public string SecretKey { get; set; } = "minioadmin";
    public string Bucket { get; set; } = "nodereel";
    public bool UseSsl { get; set; }
}

public sealed class FfmpegOptions
{
    public const string SectionName = "Ffmpeg";
    public string BinaryPath { get; set; } = "ffmpeg";
    public string TempDirectory { get; set; } = Path.Combine(Path.GetTempPath(), "nodereel");
}
