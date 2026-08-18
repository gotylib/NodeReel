using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NodeReel.Application.Abstractions;
using NodeReel.Infrastructure.Options;

namespace NodeReel.Infrastructure.Video;

public sealed class FfmpegVideoProcessor : IVideoProcessor
{
    private readonly FfmpegOptions _options;
    private readonly ILogger<FfmpegVideoProcessor> _logger;
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(2);
    private static string? _resolvedFfmpegPath;
    private static bool _ffmpegResolved;

    public FfmpegVideoProcessor(IOptions<FfmpegOptions> options, ILogger<FfmpegVideoProcessor> logger)
    {
        _options = options.Value;
        if (string.IsNullOrWhiteSpace(_options.TempDirectory))
            _options.TempDirectory = Path.Combine(Path.GetTempPath(), "nodereel");
        _logger = logger;
        Directory.CreateDirectory(_options.TempDirectory);
    }

    public Task StripMetadataAsync(string inputPath, string outputPath, CancellationToken ct = default) =>
        RunAsync([
            "-y",
            "-hide_banner",
            "-loglevel", "error",
            "-i", inputPath,
            "-map_metadata", "-1",
            "-c", "copy",
            outputPath
        ], ct);

    public Task AddInvisibleNoiseAsync(string inputPath, string outputPath, double strength, CancellationToken ct = default)
    {
        strength = Math.Clamp(strength, 0.1, 20);
        var filter = $"noise=alls={strength:0.##}:allf=t";
        return RunAsync([
            "-y",
            "-hide_banner",
            "-loglevel", "error",
            "-i", inputPath,
            "-vf", filter,
            "-c:v", "libx264",
            "-preset", "ultrafast",
            "-crf", "23",
            "-c:a", "copy",
            "-map_metadata", "-1",
            outputPath
        ], ct);
    }

    public Task TrimAsync(string inputPath, string outputPath, double startSec, double? durationSec, CancellationToken ct = default)
    {
        startSec = Math.Max(0, startSec);
        var args = new List<string>
        {
            "-y", "-hide_banner", "-loglevel", "error",
            "-ss", startSec.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
            "-i", inputPath
        };
        if (durationSec is > 0)
        {
            args.Add("-t");
            args.Add(durationSec.Value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
        }

        args.AddRange(["-c:v", "libx264", "-preset", "ultrafast", "-crf", "23", "-c:a", "aac", "-movflags", "+faststart", outputPath]);
        return RunAsync(args.ToArray(), ct);
    }

    public Task ExtractAudioAsync(string inputPath, string outputPath, CancellationToken ct = default) =>
        RunAsync([
            "-y", "-hide_banner", "-loglevel", "error",
            "-i", inputPath,
            "-vn",
            "-c:a", "libmp3lame",
            "-q:a", "2",
            outputPath
        ], ct);

    public Task RemoveAudioAsync(string inputPath, string outputPath, CancellationToken ct = default) =>
        RunAsync([
            "-y", "-hide_banner", "-loglevel", "error",
            "-i", inputPath,
            "-an",
            "-c:v", "copy",
            outputPath
        ], ct);

    public async Task ChangeSpeedAsync(string inputPath, string outputPath, double speed, CancellationToken ct = default)
    {
        speed = Math.Clamp(speed, 0.25, 4.0);
        var inv = (1.0 / speed).ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);
        var atempo = BuildAtempoChain(speed);
        try
        {
            await RunAsync([
                "-y", "-hide_banner", "-loglevel", "error",
                "-i", inputPath,
                "-filter_complex", $"[0:v]setpts={inv}*PTS[v];[0:a]{atempo}[a]",
                "-map", "[v]",
                "-map", "[a]",
                "-c:v", "libx264",
                "-preset", "ultrafast",
                "-crf", "23",
                "-c:a", "aac",
                outputPath
            ], ct);
        }
        catch (InvalidOperationException)
        {
            // No audio stream — speed video only.
            await RunAsync([
                "-y", "-hide_banner", "-loglevel", "error",
                "-i", inputPath,
                "-vf", $"setpts={inv}*PTS",
                "-an",
                "-c:v", "libx264",
                "-preset", "ultrafast",
                "-crf", "23",
                outputPath
            ], ct);
        }
    }

    public Task ResizeVideoAsync(string inputPath, string outputPath, int width, int height, CancellationToken ct = default)
    {
        width = Math.Clamp(width, 16, 7680);
        height = Math.Clamp(height, 16, 4320);
        // force even dimensions for libx264
        if (width % 2 != 0) width--;
        if (height % 2 != 0) height--;
        return RunAsync([
            "-y", "-hide_banner", "-loglevel", "error",
            "-i", inputPath,
            "-vf", $"scale={width}:{height}",
            "-c:v", "libx264",
            "-preset", "ultrafast",
            "-crf", "23",
            "-c:a", "copy",
            outputPath
        ], ct);
    }

    public Task ExtractFrameAsync(string inputPath, string outputPath, double timeSec, CancellationToken ct = default)
    {
        timeSec = Math.Max(0, timeSec);
        return RunAsync([
            "-y", "-hide_banner", "-loglevel", "error",
            "-ss", timeSec.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
            "-i", inputPath,
            "-frames:v", "1",
            "-q:v", "2",
            outputPath
        ], ct);
    }

    public Task RotateVideoAsync(string inputPath, string outputPath, int degrees, CancellationToken ct = default)
    {
        var transpose = degrees switch
        {
            90 => "transpose=1",
            180 => "transpose=1,transpose=1",
            270 => "transpose=2",
            _ => throw new ArgumentOutOfRangeException(nameof(degrees), "Supported rotations: 90, 180, 270.")
        };
        return RunAsync([
            "-y", "-hide_banner", "-loglevel", "error",
            "-i", inputPath,
            "-vf", transpose,
            "-c:v", "libx264",
            "-preset", "ultrafast",
            "-crf", "23",
            "-c:a", "copy",
            outputPath
        ], ct);
    }

    public Task SetVolumeAsync(string inputPath, string outputPath, double volume, CancellationToken ct = default)
    {
        volume = Math.Clamp(volume, 0, 4);
        var vol = volume.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);
        return RunAsync([
            "-y", "-hide_banner", "-loglevel", "error",
            "-i", inputPath,
            "-af", $"volume={vol}",
            "-c:v", "copy",
            "-c:a", "aac",
            outputPath
        ], ct);
    }

    public Task ResizeImageAsync(string inputPath, string outputPath, int width, int height, CancellationToken ct = default)
    {
        width = Math.Clamp(width, 1, 10000);
        height = Math.Clamp(height, 1, 10000);
        return RunAsync([
            "-y", "-hide_banner", "-loglevel", "error",
            "-i", inputPath,
            "-vf", $"scale={width}:{height}",
            outputPath
        ], ct);
    }

    public Task CropImageAsync(string inputPath, string outputPath, int x, int y, int width, int height, CancellationToken ct = default)
    {
        x = Math.Max(0, x);
        y = Math.Max(0, y);
        width = Math.Clamp(width, 1, 10000);
        height = Math.Clamp(height, 1, 10000);
        return RunAsync([
            "-y", "-hide_banner", "-loglevel", "error",
            "-i", inputPath,
            "-vf", $"crop={width}:{height}:{x}:{y}",
            outputPath
        ], ct);
    }

    public Task BlurImageAsync(string inputPath, string outputPath, double sigma, CancellationToken ct = default)
    {
        sigma = Math.Clamp(sigma, 0.1, 50);
        var s = sigma.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        return RunAsync([
            "-y", "-hide_banner", "-loglevel", "error",
            "-i", inputPath,
            "-vf", $"gblur=sigma={s}",
            outputPath
        ], ct);
    }

    public Task ImageToVideoAsync(string inputPath, string outputPath, double durationSec, int fps, CancellationToken ct = default)
    {
        durationSec = Math.Clamp(durationSec, 0.1, 600);
        fps = Math.Clamp(fps, 1, 60);
        return RunAsync([
            "-y", "-hide_banner", "-loglevel", "error",
            "-loop", "1",
            "-i", inputPath,
            "-t", durationSec.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
            "-r", fps.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "-c:v", "libx264",
            "-preset", "ultrafast",
            "-pix_fmt", "yuv420p",
            "-vf", "scale=trunc(iw/2)*2:trunc(ih/2)*2",
            outputPath
        ], ct);
    }

    public Task TrimAudioAsync(string inputPath, string outputPath, double startSec, double? durationSec, CancellationToken ct = default)
    {
        startSec = Math.Max(0, startSec);
        var args = new List<string>
        {
            "-y", "-hide_banner", "-loglevel", "error",
            "-ss", startSec.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
            "-i", inputPath
        };
        if (durationSec is > 0)
        {
            args.Add("-t");
            args.Add(durationSec.Value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
        }
        args.AddRange(["-vn", "-c:a", "libmp3lame", "-q:a", "2", outputPath]);
        return RunAsync(args.ToArray(), ct);
    }

    public Task SetAudioVolumeAsync(string inputPath, string outputPath, double volume, CancellationToken ct = default)
    {
        volume = Math.Clamp(volume, 0, 4);
        var vol = volume.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);
        return RunAsync([
            "-y", "-hide_banner", "-loglevel", "error",
            "-i", inputPath,
            "-af", $"volume={vol}",
            "-vn",
            "-c:a", "libmp3lame", "-q:a", "2",
            outputPath
        ], ct);
    }

    public Task ChangeAudioSpeedAsync(string inputPath, string outputPath, double speed, CancellationToken ct = default)
    {
        speed = Math.Clamp(speed, 0.25, 4.0);
        var atempo = BuildAtempoChain(speed);
        return RunAsync([
            "-y", "-hide_banner", "-loglevel", "error",
            "-i", inputPath,
            "-af", atempo,
            "-vn",
            "-c:a", "libmp3lame", "-q:a", "2",
            outputPath
        ], ct);
    }

    public Task FadeAudioAsync(string inputPath, string outputPath, double fadeInSec, double fadeOutSec, CancellationToken ct = default)
    {
        fadeInSec = Math.Clamp(fadeInSec, 0, 60);
        fadeOutSec = Math.Clamp(fadeOutSec, 0, 60);
        var filters = new List<string>();
        if (fadeInSec > 0)
        {
            var d = fadeInSec.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
            filters.Add($"afade=t=in:st=0:d={d}");
        }
        if (fadeOutSec > 0)
        {
            // Reverse + fade-in + reverse ≈ fade-out at the end without probing duration.
            var d = fadeOutSec.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
            filters.Add($"areverse,afade=t=in:st=0:d={d},areverse");
        }
        if (filters.Count == 0)
            filters.Add("anull");

        return RunAsync([
            "-y", "-hide_banner", "-loglevel", "error",
            "-i", inputPath,
            "-af", string.Join(",", filters),
            "-vn",
            "-c:a", "libmp3lame", "-q:a", "2",
            outputPath
        ], ct);
    }

    public Task ReverseAudioAsync(string inputPath, string outputPath, CancellationToken ct = default) =>
        RunAsync([
            "-y", "-hide_banner", "-loglevel", "error",
            "-i", inputPath,
            "-af", "areverse",
            "-vn",
            "-c:a", "libmp3lame", "-q:a", "2",
            outputPath
        ], ct);

    public Task AudioToVideoAsync(string inputPath, string outputPath, int width, int height, CancellationToken ct = default)
    {
        width = Math.Clamp(width, 16, 3840);
        height = Math.Clamp(height, 16, 2160);
        if (width % 2 != 0) width--;
        if (height % 2 != 0) height--;
        return RunAsync([
            "-y", "-hide_banner", "-loglevel", "error",
            "-f", "lavfi", "-i", $"color=c=black:s={width}x{height}:r=30",
            "-i", inputPath,
            "-shortest",
            "-c:v", "libx264", "-preset", "ultrafast", "-pix_fmt", "yuv420p",
            "-c:a", "aac",
            "-map", "0:v:0", "-map", "1:a:0",
            outputPath
        ], ct);
    }

    public Task CropVideoAsync(string inputPath, string outputPath, int x, int y, int width, int height, CancellationToken ct = default)
    {
        x = Math.Max(0, x);
        y = Math.Max(0, y);
        width = Math.Clamp(width, 2, 7680);
        height = Math.Clamp(height, 2, 4320);
        if (width % 2 != 0) width--;
        if (height % 2 != 0) height--;
        return RunAsync([
            "-y", "-hide_banner", "-loglevel", "error",
            "-i", inputPath,
            "-vf", $"crop={width}:{height}:{x}:{y}",
            "-c:v", "libx264", "-preset", "ultrafast", "-crf", "23",
            "-c:a", "copy",
            outputPath
        ], ct);
    }

    public Task FlipVideoAsync(string inputPath, string outputPath, bool horizontal, CancellationToken ct = default)
    {
        var vf = horizontal ? "hflip" : "vflip";
        return RunAsync([
            "-y", "-hide_banner", "-loglevel", "error",
            "-i", inputPath,
            "-vf", vf,
            "-c:v", "libx264", "-preset", "ultrafast", "-crf", "23",
            "-c:a", "copy",
            outputPath
        ], ct);
    }

    public async Task ReverseVideoAsync(string inputPath, string outputPath, CancellationToken ct = default)
    {
        try
        {
            await RunAsync([
                "-y", "-hide_banner", "-loglevel", "error",
                "-i", inputPath,
                "-vf", "reverse",
                "-af", "areverse",
                "-c:v", "libx264", "-preset", "ultrafast", "-crf", "23",
                "-c:a", "aac",
                outputPath
            ], ct, TimeSpan.FromMinutes(8));
        }
        catch (InvalidOperationException)
        {
            await RunAsync([
                "-y", "-hide_banner", "-loglevel", "error",
                "-i", inputPath,
                "-vf", "reverse",
                "-an",
                "-c:v", "libx264", "-preset", "ultrafast", "-crf", "23",
                outputPath
            ], ct, TimeSpan.FromMinutes(8));
        }
    }

    public Task RotateImageAsync(string inputPath, string outputPath, int degrees, CancellationToken ct = default)
    {
        var filter = degrees switch
        {
            90 => "transpose=1",
            180 => "transpose=1,transpose=1",
            270 => "transpose=2",
            _ => throw new ArgumentOutOfRangeException(nameof(degrees), "Supported rotations: 90, 180, 270.")
        };
        return RunAsync([
            "-y", "-hide_banner", "-loglevel", "error",
            "-i", inputPath,
            "-vf", filter,
            outputPath
        ], ct);
    }

    public Task FlipImageAsync(string inputPath, string outputPath, bool horizontal, CancellationToken ct = default)
    {
        var vf = horizontal ? "hflip" : "vflip";
        return RunAsync([
            "-y", "-hide_banner", "-loglevel", "error",
            "-i", inputPath,
            "-vf", vf,
            outputPath
        ], ct);
    }

    public Task GrayscaleImageAsync(string inputPath, string outputPath, CancellationToken ct = default) =>
        RunAsync([
            "-y", "-hide_banner", "-loglevel", "error",
            "-i", inputPath,
            "-vf", "hue=s=0",
            outputPath
        ], ct);

    public async Task ConcatVideosAsync(IReadOnlyList<string> inputPaths, string outputPath, CancellationToken ct = default)
    {
        if (inputPaths is null || inputPaths.Count == 0)
            throw new ArgumentException("At least one video is required for merge.");

        if (inputPaths.Count == 1)
        {
            File.Copy(inputPaths[0], outputPath, overwrite: true);
            return;
        }

        var workId = Guid.NewGuid().ToString("N");
        var normalized = new List<string>();
        try
        {
            for (var i = 0; i < inputPaths.Count; i++)
            {
                var normPath = Path.Combine(_options.TempDirectory, $"{workId}_n{i}.mp4");
                await NormalizeForConcatAsync(inputPaths[i], normPath, ct);
                normalized.Add(normPath);
            }

            var args = new List<string> { "-y", "-hide_banner", "-loglevel", "error" };
            foreach (var path in normalized)
            {
                args.Add("-i");
                args.Add(path);
            }

            var n = normalized.Count;
            var parts = string.Concat(Enumerable.Range(0, n).Select(i => $"[{i}:v][{i}:a]"));
            var filter = $"{parts}concat=n={n}:v=1:a=1[v][a]";
            args.AddRange([
                "-filter_complex", filter,
                "-map", "[v]",
                "-map", "[a]",
                "-c:v", "libx264",
                "-preset", "ultrafast",
                "-crf", "23",
                "-c:a", "aac",
                "-movflags", "+faststart",
                outputPath
            ]);

            await RunAsync(args.ToArray(), ct, TimeSpan.FromMinutes(8));
        }
        finally
        {
            foreach (var path in normalized)
                TryDelete(path);
        }
    }

    private async Task NormalizeForConcatAsync(string inputPath, string outputPath, CancellationToken ct)
    {
        const string vf =
            "scale=1280:720:force_original_aspect_ratio=decrease,pad=1280:720:(ow-iw)/2:(oh-ih)/2,setsar=1,fps=30,format=yuv420p";

        try
        {
            await RunAsync([
                "-y", "-hide_banner", "-loglevel", "error",
                "-i", inputPath,
                "-vf", vf,
                "-c:v", "libx264", "-preset", "ultrafast", "-crf", "23",
                "-c:a", "aac", "-ar", "44100", "-ac", "2",
                "-map", "0:v:0", "-map", "0:a:0",
                outputPath
            ], ct, TimeSpan.FromMinutes(4));
        }
        catch (InvalidOperationException)
        {
            // No audio track — add silent audio so concat stays consistent.
            await RunAsync([
                "-y", "-hide_banner", "-loglevel", "error",
                "-i", inputPath,
                "-f", "lavfi", "-i", "anullsrc=channel_layout=stereo:sample_rate=44100",
                "-vf", vf,
                "-c:v", "libx264", "-preset", "ultrafast", "-crf", "23",
                "-c:a", "aac", "-ar", "44100", "-ac", "2",
                "-map", "0:v:0", "-map", "1:a:0",
                "-shortest",
                outputPath
            ], ct, TimeSpan.FromMinutes(4));
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
            // ignore
        }
    }

    private static string BuildAtempoChain(double speed)
    {
        // atempo filter range is 0.5–2.0
        var factors = new List<double>();
        var remaining = speed;
        while (remaining > 2.0)
        {
            factors.Add(2.0);
            remaining /= 2.0;
        }
        while (remaining < 0.5)
        {
            factors.Add(0.5);
            remaining /= 0.5;
        }
        factors.Add(remaining);
        return string.Join(",", factors.Select(f =>
            $"atempo={f.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture)}"));
    }

    private async Task RunAsync(string[] args, CancellationToken ct, TimeSpan? timeout = null)
    {
        var ffmpeg = ResolveNativeFfmpegPath();
        if (!string.IsNullOrEmpty(ffmpeg))
        {
            await RunProcessAsync(ffmpeg, args, ct, timeout);
            return;
        }

        if (IsCommandAvailable("docker"))
        {
            _logger.LogWarning("Native FFmpeg not found; using Docker image mwader/static-ffmpeg");
            await RunViaDockerAsync(args, ct, timeout);
            return;
        }

        throw new InvalidOperationException(
            "FFmpeg is not installed in this container, and Docker is not available as a fallback. " +
            "Build the API with backend/Dockerfile (it installs ffmpeg) or add ffmpeg to the image. " +
            "Tried: " + string.Join(", ", FfmpegCandidates()));
    }

    internal string? ProbeFfmpegPath() => ResolveNativeFfmpegPath();

    private async Task RunViaDockerAsync(string[] args, CancellationToken ct, TimeSpan? timeout = null)
    {
        var hostTemp = Path.GetFullPath(_options.TempDirectory);
        Directory.CreateDirectory(hostTemp);

        var dockerArgs = new List<string>
        {
            "run", "--rm",
            "-v", $"{hostTemp}:/work",
            "mwader/static-ffmpeg:7.1"
        };

        foreach (var arg in args)
        {
            if (arg.StartsWith(hostTemp, StringComparison.OrdinalIgnoreCase))
            {
                var relative = Path.GetRelativePath(hostTemp, arg).Replace('\\', '/');
                dockerArgs.Add($"/work/{relative}");
            }
            else
            {
                dockerArgs.Add(arg);
            }
        }

        await RunProcessAsync("docker", dockerArgs.ToArray(), ct, timeout);
    }
    private string? ResolveNativeFfmpegPath()
    {
        if (_ffmpegResolved)
            return _resolvedFfmpegPath;

        foreach (var candidate in FfmpegCandidates())
        {
            if (TryProbeCommand(candidate, "-version"))
            {
                _resolvedFfmpegPath = candidate;
                _ffmpegResolved = true;
                _logger.LogInformation("Using FFmpeg at {Path}", candidate);
                return candidate;
            }
        }

        _ffmpegResolved = true;
        _resolvedFfmpegPath = null;
        _logger.LogWarning("FFmpeg not found. Candidates: {Candidates}", string.Join(", ", FfmpegCandidates()));
        return null;
    }

    private IEnumerable<string> FfmpegCandidates()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var raw in new[]
        {
            _options.BinaryPath,
            "/usr/local/bin/ffmpeg",
            "/usr/bin/ffmpeg",
            "ffmpeg"
        })
        {
            if (string.IsNullOrWhiteSpace(raw))
                continue;
            var path = raw.Trim();
            if (seen.Add(path))
                yield return path;
        }
    }

    private static bool IsCommandAvailable(string fileName) => TryProbeCommand(fileName, "--version")
        || TryProbeCommand(fileName, "-v");

    private static bool TryProbeCommand(string fileName, string versionArg)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                ArgumentList = { versionArg },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            if (p is null)
                return false;

            if (!p.WaitForExit(3000))
            {
                TryKill(p);
                return false;
            }

            return p.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private async Task RunProcessAsync(string fileName, string[] args, CancellationToken ct, TimeSpan? timeout = null)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            // Do NOT redirect stdout — FFmpeg can fill the pipe and deadlock forever.
            RedirectStandardError = true,
            RedirectStandardOutput = false,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var arg in args)
            psi.ArgumentList.Add(arg);

        _logger.LogInformation("Running {File} {Args}", fileName, string.Join(' ', args));

        using var process = new Process { StartInfo = psi };
        var stderr = new StringBuilder();
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null) stderr.AppendLine(e.Data);
        };

        try
        {
            if (!process.Start())
                throw new InvalidOperationException($"Failed to start '{fileName}'.");
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            throw new InvalidOperationException(
                $"Cannot start '{fileName}'. Install FFmpeg (PATH) or Docker. ({ex.Message})");
        }

        process.BeginErrorReadLine();

        var limit = timeout ?? DefaultTimeout;
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(limit);

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            TryKill(process);
            throw new TimeoutException($"FFmpeg timed out after {limit.TotalSeconds:0}s.");
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }

        if (process.ExitCode != 0)
        {
            if (args.Contains("-c") && args.Contains("copy") && fileName != "docker")
            {
                _logger.LogWarning("FFmpeg copy failed, retrying with re-encode. {Error}", stderr.ToString());
                var retryArgs = args.ToList();
                var copyIdx = retryArgs.IndexOf("copy");
                if (copyIdx > 0 && retryArgs[copyIdx - 1] == "-c")
                {
                    retryArgs.RemoveAt(copyIdx);
                    retryArgs[copyIdx - 1] = "-c:v";
                    retryArgs.Insert(copyIdx, "libx264");
                    retryArgs.Insert(copyIdx + 1, "-preset");
                    retryArgs.Insert(copyIdx + 2, "ultrafast");
                    retryArgs.Insert(copyIdx + 3, "-crf");
                    retryArgs.Insert(copyIdx + 4, "23");
                    retryArgs.Insert(copyIdx + 5, "-c:a");
                    retryArgs.Insert(copyIdx + 6, "aac");
                    await RunAsync(retryArgs.ToArray(), ct, timeout);
                    return;
                }
            }

            // Also allow docker path to retry re-encode
            if (args.Contains("-c") && args.Contains("copy"))
            {
                _logger.LogWarning("FFmpeg copy failed (docker), retrying with re-encode. {Error}", stderr.ToString());
                var retryArgs = args.ToList();
                var copyIdx = retryArgs.IndexOf("copy");
                if (copyIdx > 0 && retryArgs[copyIdx - 1] == "-c")
                {
                    retryArgs.RemoveAt(copyIdx);
                    retryArgs[copyIdx - 1] = "-c:v";
                    retryArgs.Insert(copyIdx, "libx264");
                    retryArgs.Insert(copyIdx + 1, "-preset");
                    retryArgs.Insert(copyIdx + 2, "ultrafast");
                    retryArgs.Insert(copyIdx + 3, "-crf");
                    retryArgs.Insert(copyIdx + 4, "23");
                    retryArgs.Insert(copyIdx + 5, "-c:a");
                    retryArgs.Insert(copyIdx + 6, "aac");
                    await RunAsync(retryArgs.ToArray(), ct, timeout);
                    return;
                }
            }

            throw new InvalidOperationException($"FFmpeg failed ({process.ExitCode}): {stderr}");
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch
        {
            // ignore
        }
    }
}
