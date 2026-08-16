using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NodeReel.Application.Abstractions;
using NodeReel.Infrastructure.Options;

namespace NodeReel.Infrastructure.Video;

public sealed class YtDlpSocialDownloader : ISocialVideoDownloader
{
    private readonly YtDlpOptions _options;
    private readonly ILogger<YtDlpSocialDownloader> _logger;
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(15);

    public YtDlpSocialDownloader(IOptions<YtDlpOptions> options, ILogger<YtDlpSocialDownloader> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task DownloadAsync(string url, string outputPath, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new InvalidOperationException("URL is required.");
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new InvalidOperationException("URL must be an absolute http(s) link.");

        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var binary = string.IsNullOrWhiteSpace(_options.BinaryPath) ? "yt-dlp" : _options.BinaryPath.Trim();
        var args = new[]
        {
            "--no-playlist",
            "--no-warnings",
            "-f", "bv*[ext=mp4]+ba[ext=m4a]/b[ext=mp4]/bv*+ba/b",
            "--merge-output-format", "mp4",
            "-o", outputPath,
            "--",
            uri.ToString()
        };

        _logger.LogInformation("yt-dlp download starting for {Host}", uri.Host);

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = binary,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        foreach (var arg in args)
            process.StartInfo.ArgumentList.Add(arg);

        var stderr = new StringBuilder();
        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                stderr.AppendLine(e.Data);
        };

        if (!process.Start())
            throw new InvalidOperationException($"Failed to start yt-dlp ('{binary}'). Is it installed and on PATH?");

        process.BeginErrorReadLine();
        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(DefaultTimeout);

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
            await stdoutTask;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            TryKill(process);
            throw new TimeoutException("yt-dlp timed out while downloading the video.");
        }
        catch
        {
            TryKill(process);
            throw;
        }

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"yt-dlp failed ({process.ExitCode}): {stderr}");

        if (!File.Exists(outputPath) || new FileInfo(outputPath).Length == 0)
            throw new InvalidOperationException("yt-dlp finished but produced no video file.");
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
