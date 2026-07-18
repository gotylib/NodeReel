using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NodeReel.Application.Abstractions;
using NodeReel.Application.Nodes;
using NodeReel.Infrastructure.Options;

namespace NodeReel.Infrastructure.Nodes;

public sealed class LocalNodeExecutor
{
    private readonly IObjectStorage _storage;
    private readonly IVideoProcessor _video;
    private readonly FfmpegOptions _ffmpegOptions;
    private readonly ILogger<LocalNodeExecutor> _logger;

    public LocalNodeExecutor(
        IObjectStorage storage,
        IVideoProcessor video,
        IOptions<FfmpegOptions> ffmpegOptions,
        ILogger<LocalNodeExecutor> logger)
    {
        _storage = storage;
        _video = video;
        _ffmpegOptions = ffmpegOptions.Value;
        if (string.IsNullOrWhiteSpace(_ffmpegOptions.TempDirectory))
            _ffmpegOptions.TempDirectory = Path.Combine(Path.GetTempPath(), "nodereel");
        _logger = logger;
        Directory.CreateDirectory(_ffmpegOptions.TempDirectory);
    }

    public static IReadOnlyList<NodeDescriptor> Descriptors { get; } =
    [
        new NodeDescriptor
        {
            Id = LocalNodeIds.UploadVideo,
            ProviderId = LocalNodeIds.ProviderId,
            Name = "Upload video",
            Category = "input",
            Description = "Provides an already uploaded video object as pipeline input.",
            Icon = "upload",
            Subtitle = "file",
            Inputs = [],
            Outputs = [new NodePort { Name = "video", Type = "video", Required = true }],
            ParamsSchema = NodeSchemaHelper.UploadParamsSchema("video")
        },
        new NodeDescriptor
        {
            Id = LocalNodeIds.UploadImage,
            ProviderId = LocalNodeIds.ProviderId,
            Name = "Upload image",
            Category = "input",
            Description = "Provides an already uploaded image object as pipeline input.",
            Icon = "image",
            Subtitle = "file",
            Inputs = [],
            Outputs = [new NodePort { Name = "image", Type = "image", Required = true }],
            ParamsSchema = NodeSchemaHelper.UploadParamsSchema("image")
        },
        new NodeDescriptor
        {
            Id = LocalNodeIds.UploadAudio,
            ProviderId = LocalNodeIds.ProviderId,
            Name = "Upload audio",
            Category = "input",
            Description = "Provides an already uploaded audio object as pipeline input.",
            Icon = "audio",
            Subtitle = "file",
            Inputs = [],
            Outputs = [new NodePort { Name = "audio", Type = "audio", Required = true }],
            ParamsSchema = NodeSchemaHelper.UploadParamsSchema("audio")
        },
        new NodeDescriptor
        {
            Id = LocalNodeIds.StripMetadata,
            ProviderId = LocalNodeIds.ProviderId,
            Name = "Strip metadata",
            Category = "video",
            Description = "Removes container/stream metadata from a video.",
            Icon = "strip",
            Subtitle = "ffmpeg",
            Inputs = [new NodePort { Name = "video", Type = "video", Required = true }],
            Outputs = [new NodePort { Name = "video", Type = "video", Required = true }],
            ParamsSchema = NodeSchemaHelper.EmptyObjectSchema()
        },
        new NodeDescriptor
        {
            Id = LocalNodeIds.AddInvisibleNoise,
            ProviderId = LocalNodeIds.ProviderId,
            Name = "Invisible noise",
            Category = "video",
            Description = "Applies subtle luma/chroma noise that is hard to notice visually.",
            Icon = "noise",
            Subtitle = "ffmpeg",
            Inputs = [new NodePort { Name = "video", Type = "video", Required = true }],
            Outputs = [new NodePort { Name = "video", Type = "video", Required = true }],
            ParamsSchema = NodeSchemaHelper.NoiseParamsSchema()
        },
        new NodeDescriptor
        {
            Id = LocalNodeIds.TrimVideo,
            ProviderId = LocalNodeIds.ProviderId,
            Name = "Trim video",
            Category = "video",
            Description = "Cuts a segment from the video by start time and optional duration.",
            Icon = "trim",
            Subtitle = "ffmpeg",
            Inputs = [new NodePort { Name = "video", Type = "video", Required = true }],
            Outputs = [new NodePort { Name = "video", Type = "video", Required = true }],
            ParamsSchema = NodeSchemaHelper.TrimParamsSchema()
        },
        new NodeDescriptor
        {
            Id = LocalNodeIds.ExtractAudio,
            ProviderId = LocalNodeIds.ProviderId,
            Name = "Extract audio",
            Category = "video",
            Description = "Extracts the audio track as an MP3 file.",
            Icon = "audio",
            Subtitle = "ffmpeg",
            Inputs = [new NodePort { Name = "video", Type = "video", Required = true }],
            Outputs = [new NodePort { Name = "audio", Type = "audio", Required = true }],
            ParamsSchema = NodeSchemaHelper.EmptyObjectSchema()
        },
        new NodeDescriptor
        {
            Id = LocalNodeIds.RemoveAudio,
            ProviderId = LocalNodeIds.ProviderId,
            Name = "Remove audio",
            Category = "video",
            Description = "Removes the audio track from the video.",
            Icon = "mute",
            Subtitle = "ffmpeg",
            Inputs = [new NodePort { Name = "video", Type = "video", Required = true }],
            Outputs = [new NodePort { Name = "video", Type = "video", Required = true }],
            ParamsSchema = NodeSchemaHelper.EmptyObjectSchema()
        },
        new NodeDescriptor
        {
            Id = LocalNodeIds.ChangeSpeed,
            ProviderId = LocalNodeIds.ProviderId,
            Name = "Change speed",
            Category = "video",
            Description = "Speeds up or slows down video (and audio when present).",
            Icon = "speed",
            Subtitle = "ffmpeg",
            Inputs = [new NodePort { Name = "video", Type = "video", Required = true }],
            Outputs = [new NodePort { Name = "video", Type = "video", Required = true }],
            ParamsSchema = NodeSchemaHelper.SpeedParamsSchema()
        },
        new NodeDescriptor
        {
            Id = LocalNodeIds.ResizeVideo,
            ProviderId = LocalNodeIds.ProviderId,
            Name = "Resize video",
            Category = "video",
            Description = "Scales video to the given width and height.",
            Icon = "resize",
            Subtitle = "ffmpeg",
            Inputs = [new NodePort { Name = "video", Type = "video", Required = true }],
            Outputs = [new NodePort { Name = "video", Type = "video", Required = true }],
            ParamsSchema = NodeSchemaHelper.SizeParamsSchema(1280, 720)
        },
        new NodeDescriptor
        {
            Id = LocalNodeIds.ExtractFrame,
            ProviderId = LocalNodeIds.ProviderId,
            Name = "Extract frame",
            Category = "video",
            Description = "Grabs a still image frame at a given timestamp.",
            Icon = "frame",
            Subtitle = "ffmpeg",
            Inputs = [new NodePort { Name = "video", Type = "video", Required = true }],
            Outputs = [new NodePort { Name = "image", Type = "image", Required = true }],
            ParamsSchema = NodeSchemaHelper.FrameParamsSchema()
        },
        new NodeDescriptor
        {
            Id = LocalNodeIds.RotateVideo,
            ProviderId = LocalNodeIds.ProviderId,
            Name = "Rotate video",
            Category = "video",
            Description = "Rotates video by 90, 180, or 270 degrees.",
            Icon = "rotate",
            Subtitle = "ffmpeg",
            Inputs = [new NodePort { Name = "video", Type = "video", Required = true }],
            Outputs = [new NodePort { Name = "video", Type = "video", Required = true }],
            ParamsSchema = NodeSchemaHelper.RotateParamsSchema()
        },
        new NodeDescriptor
        {
            Id = LocalNodeIds.SetVolume,
            ProviderId = LocalNodeIds.ProviderId,
            Name = "Set volume",
            Category = "video",
            Description = "Changes audio volume without re-encoding video.",
            Icon = "volume",
            Subtitle = "ffmpeg",
            Inputs = [new NodePort { Name = "video", Type = "video", Required = true }],
            Outputs = [new NodePort { Name = "video", Type = "video", Required = true }],
            ParamsSchema = NodeSchemaHelper.VolumeParamsSchema()
        },
        new NodeDescriptor
        {
            Id = LocalNodeIds.CropVideo,
            ProviderId = LocalNodeIds.ProviderId,
            Name = "Crop video",
            Category = "video",
            Description = "Crops a rectangular region from the video frame.",
            Icon = "crop",
            Subtitle = "ffmpeg",
            Inputs = [new NodePort { Name = "video", Type = "video", Required = true }],
            Outputs = [new NodePort { Name = "video", Type = "video", Required = true }],
            ParamsSchema = NodeSchemaHelper.CropParamsSchema()
        },
        new NodeDescriptor
        {
            Id = LocalNodeIds.FlipVideo,
            ProviderId = LocalNodeIds.ProviderId,
            Name = "Flip video",
            Category = "video",
            Description = "Flips video horizontally or vertically.",
            Icon = "flip",
            Subtitle = "ffmpeg",
            Inputs = [new NodePort { Name = "video", Type = "video", Required = true }],
            Outputs = [new NodePort { Name = "video", Type = "video", Required = true }],
            ParamsSchema = NodeSchemaHelper.FlipParamsSchema()
        },
        new NodeDescriptor
        {
            Id = LocalNodeIds.ReverseVideo,
            ProviderId = LocalNodeIds.ProviderId,
            Name = "Reverse video",
            Category = "video",
            Description = "Plays the video (and audio) backwards.",
            Icon = "reverse",
            Subtitle = "ffmpeg",
            Inputs = [new NodePort { Name = "video", Type = "video", Required = true }],
            Outputs = [new NodePort { Name = "video", Type = "video", Required = true }],
            ParamsSchema = NodeSchemaHelper.EmptyObjectSchema()
        },
        new NodeDescriptor
        {
            Id = LocalNodeIds.ResizeImage,
            ProviderId = LocalNodeIds.ProviderId,
            Name = "Resize image",
            Category = "image",
            Description = "Scales an image to the given dimensions.",
            Icon = "resize",
            Subtitle = "ffmpeg",
            Inputs = [new NodePort { Name = "image", Type = "image", Required = true }],
            Outputs = [new NodePort { Name = "image", Type = "image", Required = true }],
            ParamsSchema = NodeSchemaHelper.SizeParamsSchema(1024, 1024)
        },
        new NodeDescriptor
        {
            Id = LocalNodeIds.CropImage,
            ProviderId = LocalNodeIds.ProviderId,
            Name = "Crop image",
            Category = "image",
            Description = "Crops a rectangular region from an image.",
            Icon = "crop",
            Subtitle = "ffmpeg",
            Inputs = [new NodePort { Name = "image", Type = "image", Required = true }],
            Outputs = [new NodePort { Name = "image", Type = "image", Required = true }],
            ParamsSchema = NodeSchemaHelper.CropParamsSchema()
        },
        new NodeDescriptor
        {
            Id = LocalNodeIds.BlurImage,
            ProviderId = LocalNodeIds.ProviderId,
            Name = "Blur image",
            Category = "image",
            Description = "Applies Gaussian blur to an image.",
            Icon = "blur",
            Subtitle = "ffmpeg",
            Inputs = [new NodePort { Name = "image", Type = "image", Required = true }],
            Outputs = [new NodePort { Name = "image", Type = "image", Required = true }],
            ParamsSchema = NodeSchemaHelper.BlurParamsSchema()
        },
        new NodeDescriptor
        {
            Id = LocalNodeIds.ImageToVideo,
            ProviderId = LocalNodeIds.ProviderId,
            Name = "Image to video",
            Category = "image",
            Description = "Turns a still image into a short video clip.",
            Icon = "imageVideo",
            Subtitle = "ffmpeg",
            Inputs = [new NodePort { Name = "image", Type = "image", Required = true }],
            Outputs = [new NodePort { Name = "video", Type = "video", Required = true }],
            ParamsSchema = NodeSchemaHelper.ImageToVideoParamsSchema()
        },
        new NodeDescriptor
        {
            Id = LocalNodeIds.RotateImage,
            ProviderId = LocalNodeIds.ProviderId,
            Name = "Rotate image",
            Category = "image",
            Description = "Rotates an image by 90, 180, or 270 degrees.",
            Icon = "rotate",
            Subtitle = "ffmpeg",
            Inputs = [new NodePort { Name = "image", Type = "image", Required = true }],
            Outputs = [new NodePort { Name = "image", Type = "image", Required = true }],
            ParamsSchema = NodeSchemaHelper.RotateParamsSchema()
        },
        new NodeDescriptor
        {
            Id = LocalNodeIds.FlipImage,
            ProviderId = LocalNodeIds.ProviderId,
            Name = "Flip image",
            Category = "image",
            Description = "Flips an image horizontally or vertically.",
            Icon = "flip",
            Subtitle = "ffmpeg",
            Inputs = [new NodePort { Name = "image", Type = "image", Required = true }],
            Outputs = [new NodePort { Name = "image", Type = "image", Required = true }],
            ParamsSchema = NodeSchemaHelper.FlipParamsSchema()
        },
        new NodeDescriptor
        {
            Id = LocalNodeIds.GrayscaleImage,
            ProviderId = LocalNodeIds.ProviderId,
            Name = "Grayscale image",
            Category = "image",
            Description = "Converts an image to grayscale.",
            Icon = "grayscale",
            Subtitle = "ffmpeg",
            Inputs = [new NodePort { Name = "image", Type = "image", Required = true }],
            Outputs = [new NodePort { Name = "image", Type = "image", Required = true }],
            ParamsSchema = NodeSchemaHelper.EmptyObjectSchema()
        },
        new NodeDescriptor
        {
            Id = LocalNodeIds.TrimAudio,
            ProviderId = LocalNodeIds.ProviderId,
            Name = "Trim audio",
            Category = "audio",
            Description = "Cuts a segment from an audio file.",
            Icon = "trim",
            Subtitle = "ffmpeg",
            Inputs = [new NodePort { Name = "audio", Type = "audio", Required = true }],
            Outputs = [new NodePort { Name = "audio", Type = "audio", Required = true }],
            ParamsSchema = NodeSchemaHelper.TrimParamsSchema()
        },
        new NodeDescriptor
        {
            Id = LocalNodeIds.ChangeAudioVolume,
            ProviderId = LocalNodeIds.ProviderId,
            Name = "Audio volume",
            Category = "audio",
            Description = "Changes loudness of an audio file.",
            Icon = "volume",
            Subtitle = "ffmpeg",
            Inputs = [new NodePort { Name = "audio", Type = "audio", Required = true }],
            Outputs = [new NodePort { Name = "audio", Type = "audio", Required = true }],
            ParamsSchema = NodeSchemaHelper.VolumeParamsSchema()
        },
        new NodeDescriptor
        {
            Id = LocalNodeIds.ChangeAudioSpeed,
            ProviderId = LocalNodeIds.ProviderId,
            Name = "Audio speed",
            Category = "audio",
            Description = "Speeds up or slows down audio.",
            Icon = "speed",
            Subtitle = "ffmpeg",
            Inputs = [new NodePort { Name = "audio", Type = "audio", Required = true }],
            Outputs = [new NodePort { Name = "audio", Type = "audio", Required = true }],
            ParamsSchema = NodeSchemaHelper.SpeedParamsSchema()
        },
        new NodeDescriptor
        {
            Id = LocalNodeIds.FadeAudio,
            ProviderId = LocalNodeIds.ProviderId,
            Name = "Fade audio",
            Category = "audio",
            Description = "Applies fade-in and/or fade-out to audio.",
            Icon = "fade",
            Subtitle = "ffmpeg",
            Inputs = [new NodePort { Name = "audio", Type = "audio", Required = true }],
            Outputs = [new NodePort { Name = "audio", Type = "audio", Required = true }],
            ParamsSchema = NodeSchemaHelper.FadeParamsSchema()
        },
        new NodeDescriptor
        {
            Id = LocalNodeIds.ReverseAudio,
            ProviderId = LocalNodeIds.ProviderId,
            Name = "Reverse audio",
            Category = "audio",
            Description = "Plays the audio backwards.",
            Icon = "reverse",
            Subtitle = "ffmpeg",
            Inputs = [new NodePort { Name = "audio", Type = "audio", Required = true }],
            Outputs = [new NodePort { Name = "audio", Type = "audio", Required = true }],
            ParamsSchema = NodeSchemaHelper.EmptyObjectSchema()
        },
        new NodeDescriptor
        {
            Id = LocalNodeIds.AudioToVideo,
            ProviderId = LocalNodeIds.ProviderId,
            Name = "Audio to video",
            Category = "audio",
            Description = "Wraps audio into a black video so it can continue in a video pipeline.",
            Icon = "imageVideo",
            Subtitle = "ffmpeg",
            Inputs = [new NodePort { Name = "audio", Type = "audio", Required = true }],
            Outputs = [new NodePort { Name = "video", Type = "video", Required = true }],
            ParamsSchema = NodeSchemaHelper.AudioToVideoParamsSchema()
        },
        new NodeDescriptor
        {
            Id = LocalNodeIds.If,
            ProviderId = LocalNodeIds.ProviderId,
            Name = "IF",
            Category = "flow",
            Description = "Branches the workflow. Passes video to true or false output based on condition.",
            Icon = "if",
            Subtitle = "branch",
            Inputs = [new NodePort { Name = "video", Type = "video", Required = true }],
            Outputs =
            [
                new NodePort { Name = "true", Type = "video", Required = false },
                new NodePort { Name = "false", Type = "video", Required = false }
            ],
            ParamsSchema = NodeSchemaHelper.IfParamsSchema()
        },
        new NodeDescriptor
        {
            Id = LocalNodeIds.Switch,
            ProviderId = LocalNodeIds.ProviderId,
            Name = "Switch",
            Category = "flow",
            Description = "Routes video to one of several outputs (0, 1, 2, or default).",
            Icon = "switch",
            Subtitle = "route",
            Inputs = [new NodePort { Name = "video", Type = "video", Required = true }],
            Outputs =
            [
                new NodePort { Name = "0", Type = "video", Required = false },
                new NodePort { Name = "1", Type = "video", Required = false },
                new NodePort { Name = "2", Type = "video", Required = false },
                new NodePort { Name = "default", Type = "video", Required = false }
            ],
            ParamsSchema = NodeSchemaHelper.SwitchParamsSchema()
        },
        new NodeDescriptor
        {
            Id = LocalNodeIds.Merge,
            ProviderId = LocalNodeIds.ProviderId,
            Name = "Merge",
            Category = "video",
            Description = "Concatenates connected videos in order (video0 → video1 → video2).",
            Icon = "merge",
            Subtitle = "ffmpeg",
            Inputs =
            [
                new NodePort { Name = "video0", Type = "video", Required = false },
                new NodePort { Name = "video1", Type = "video", Required = false },
                new NodePort { Name = "video2", Type = "video", Required = false }
            ],
            Outputs = [new NodePort { Name = "video", Type = "video", Required = true }],
            ParamsSchema = NodeSchemaHelper.EmptyObjectSchema()
        }
    ];

    public async Task<NodeExecuteResponse> ExecuteAsync(NodeExecuteRequest request, CancellationToken ct)
    {
        return request.NodeId switch
        {
            LocalNodeIds.UploadVideo => ExecuteUpload(request, "video"),
            LocalNodeIds.UploadImage => ExecuteUpload(request, "image"),
            LocalNodeIds.UploadAudio => ExecuteUpload(request, "audio"),
            LocalNodeIds.StripMetadata => await ExecuteStripAsync(request, ct),
            LocalNodeIds.AddInvisibleNoise => await ExecuteNoiseAsync(request, ct),
            LocalNodeIds.TrimVideo => await ExecuteTrimAsync(request, ct),
            LocalNodeIds.ExtractAudio => await ExecuteExtractAudioAsync(request, ct),
            LocalNodeIds.RemoveAudio => await ExecuteRemoveAudioAsync(request, ct),
            LocalNodeIds.ChangeSpeed => await ExecuteSpeedAsync(request, ct),
            LocalNodeIds.ResizeVideo => await ExecuteResizeVideoAsync(request, ct),
            LocalNodeIds.ExtractFrame => await ExecuteExtractFrameAsync(request, ct),
            LocalNodeIds.RotateVideo => await ExecuteRotateAsync(request, ct),
            LocalNodeIds.SetVolume => await ExecuteVolumeAsync(request, ct),
            LocalNodeIds.CropVideo => await ExecuteCropVideoAsync(request, ct),
            LocalNodeIds.FlipVideo => await ExecuteFlipVideoAsync(request, ct),
            LocalNodeIds.ReverseVideo => await ExecuteReverseVideoAsync(request, ct),
            LocalNodeIds.ResizeImage => await ExecuteResizeImageAsync(request, ct),
            LocalNodeIds.CropImage => await ExecuteCropImageAsync(request, ct),
            LocalNodeIds.BlurImage => await ExecuteBlurImageAsync(request, ct),
            LocalNodeIds.ImageToVideo => await ExecuteImageToVideoAsync(request, ct),
            LocalNodeIds.RotateImage => await ExecuteRotateImageAsync(request, ct),
            LocalNodeIds.FlipImage => await ExecuteFlipImageAsync(request, ct),
            LocalNodeIds.GrayscaleImage => await ExecuteGrayscaleImageAsync(request, ct),
            LocalNodeIds.TrimAudio => await ExecuteTrimAudioAsync(request, ct),
            LocalNodeIds.ChangeAudioVolume => await ExecuteAudioVolumeAsync(request, ct),
            LocalNodeIds.ChangeAudioSpeed => await ExecuteAudioSpeedAsync(request, ct),
            LocalNodeIds.FadeAudio => await ExecuteFadeAudioAsync(request, ct),
            LocalNodeIds.ReverseAudio => await ExecuteReverseAudioAsync(request, ct),
            LocalNodeIds.AudioToVideo => await ExecuteAudioToVideoAsync(request, ct),
            LocalNodeIds.If => ExecuteIf(request),
            LocalNodeIds.Switch => ExecuteSwitch(request),
            LocalNodeIds.Merge => await ExecuteMergeAsync(request, ct),
            _ => throw new InvalidOperationException($"Local node '{request.NodeId}' is not supported.")
        };
    }

    private static NodeExecuteResponse ExecuteUpload(NodeExecuteRequest request, string outputPort)
    {
        var key = GetParamString(request, "objectKey")
            ?? throw new InvalidOperationException($"{request.NodeId} requires params.objectKey.");

        return new NodeExecuteResponse
        {
            Outputs = new Dictionary<string, string> { [outputPort] = key },
            Logs = [$"Using uploaded object {key}"]
        };
    }

    private static NodeExecuteResponse ExecuteIf(NodeExecuteRequest request)
    {
        var video = GetInput(request, "video");
        var condition = (GetParamString(request, "condition") ?? "true").Trim().ToLowerInvariant();
        var takeTrue = condition switch
        {
            "true" or "1" or "yes" => true,
            "false" or "0" or "no" => false,
            "notempty" => !string.IsNullOrWhiteSpace(video),
            _ => string.Equals(condition, "true", StringComparison.OrdinalIgnoreCase)
        };

        var port = takeTrue ? "true" : "false";
        return new NodeExecuteResponse
        {
            Outputs = new Dictionary<string, string> { [port] = video },
            Logs = [$"IF condition '{condition}' -> {port}"]
        };
    }

    private static NodeExecuteResponse ExecuteSwitch(NodeExecuteRequest request)
    {
        var video = GetInput(request, "video");
        var route = (GetParamString(request, "route") ?? "0").Trim().ToLowerInvariant();
        var port = route switch
        {
            "0" or "1" or "2" or "default" => route,
            _ => "default"
        };

        return new NodeExecuteResponse
        {
            Outputs = new Dictionary<string, string> { [port] = video },
            Logs = [$"Switch -> {port}"]
        };
    }

    private async Task<NodeExecuteResponse> ExecuteMergeAsync(NodeExecuteRequest request, CancellationToken ct)
    {
        var keys = new List<string>();
        foreach (var name in new[] { "video0", "video1", "video2", "video" })
        {
            if (request.Inputs.TryGetValue(name, out var key) && !string.IsNullOrWhiteSpace(key))
                keys.Add(key);
        }

        if (keys.Count == 0)
            throw new InvalidOperationException("Merge has no available video inputs.");

        if (keys.Count == 1)
        {
            return new NodeExecuteResponse
            {
                Outputs = new Dictionary<string, string> { ["video"] = keys[0] },
                Logs = ["Merge received a single input — passed through."]
            };
        }

        var workId = Guid.NewGuid().ToString("N");
        var inputPaths = new List<string>();
        var outputPath = Path.Combine(_ffmpegOptions.TempDirectory, $"{workId}_merge.mp4");

        try
        {
            for (var i = 0; i < keys.Count; i++)
            {
                var path = Path.Combine(_ffmpegOptions.TempDirectory, $"{workId}_in{i}");
                await using (var download = await _storage.DownloadAsync(keys[i], ct))
                await using (var fs = File.Create(path))
                {
                    await download.CopyToAsync(fs, ct);
                }
                inputPaths.Add(path);
            }

            await _video.ConcatVideosAsync(inputPaths, outputPath, ct);

            await using var upload = File.OpenRead(outputPath);
            var outputKey = await _storage.UploadAsync(upload, "video/mp4", preferredKey: null, ct);
            _logger.LogInformation("Merged {Count} videos -> {Output}", keys.Count, outputKey);

            return new NodeExecuteResponse
            {
                Outputs = new Dictionary<string, string> { ["video"] = outputKey },
                Logs = [$"Merged {keys.Count} videos in order"]
            };
        }
        finally
        {
            foreach (var path in inputPaths)
                TryDelete(path);
            TryDelete(outputPath);
        }
    }

    private async Task<NodeExecuteResponse> ExecuteStripAsync(NodeExecuteRequest request, CancellationToken ct)
    {
        var inputKey = GetInput(request, "video");
        var outputKey = await ProcessMediaAsync(inputKey, "strip", "mp4", "video/mp4",
            (input, output) => _video.StripMetadataAsync(input, output, ct), ct);
        return OkVideo(outputKey, "Metadata stripped");
    }

    private async Task<NodeExecuteResponse> ExecuteNoiseAsync(NodeExecuteRequest request, CancellationToken ct)
    {
        var inputKey = GetInput(request, "video");
        var strength = GetParamDouble(request, "strength") ?? 2.0;
        var outputKey = await ProcessMediaAsync(inputKey, "noise", "mp4", "video/mp4",
            (input, output) => _video.AddInvisibleNoiseAsync(input, output, strength, ct), ct);
        return OkVideo(outputKey, $"Noise applied with strength={strength}");
    }

    private async Task<NodeExecuteResponse> ExecuteTrimAsync(NodeExecuteRequest request, CancellationToken ct)
    {
        var inputKey = GetInput(request, "video");
        var start = GetParamDouble(request, "startSec") ?? 0;
        var duration = GetParamDouble(request, "durationSec");
        var outputKey = await ProcessMediaAsync(inputKey, "trim", "mp4", "video/mp4",
            (input, output) => _video.TrimAsync(input, output, start, duration, ct), ct);
        return OkVideo(outputKey, $"Trimmed from {start}s" + (duration is null ? "" : $" for {duration}s"));
    }

    private async Task<NodeExecuteResponse> ExecuteExtractAudioAsync(NodeExecuteRequest request, CancellationToken ct)
    {
        var inputKey = GetInput(request, "video");
        var outputKey = await ProcessMediaAsync(inputKey, "audio", "mp3", "audio/mpeg",
            (input, output) => _video.ExtractAudioAsync(input, output, ct), ct);
        return new NodeExecuteResponse
        {
            Outputs = new Dictionary<string, string> { ["audio"] = outputKey },
            Logs = ["Audio extracted"]
        };
    }

    private async Task<NodeExecuteResponse> ExecuteRemoveAudioAsync(NodeExecuteRequest request, CancellationToken ct)
    {
        var inputKey = GetInput(request, "video");
        var outputKey = await ProcessMediaAsync(inputKey, "mute", "mp4", "video/mp4",
            (input, output) => _video.RemoveAudioAsync(input, output, ct), ct);
        return OkVideo(outputKey, "Audio removed");
    }

    private async Task<NodeExecuteResponse> ExecuteSpeedAsync(NodeExecuteRequest request, CancellationToken ct)
    {
        var inputKey = GetInput(request, "video");
        var speed = GetParamDouble(request, "speed") ?? 1.5;
        var outputKey = await ProcessMediaAsync(inputKey, "speed", "mp4", "video/mp4",
            (input, output) => _video.ChangeSpeedAsync(input, output, speed, ct), ct);
        return OkVideo(outputKey, $"Speed set to {speed}x");
    }

    private async Task<NodeExecuteResponse> ExecuteResizeVideoAsync(NodeExecuteRequest request, CancellationToken ct)
    {
        var inputKey = GetInput(request, "video");
        var width = GetParamInt(request, "width") ?? 1280;
        var height = GetParamInt(request, "height") ?? 720;
        var outputKey = await ProcessMediaAsync(inputKey, "vscale", "mp4", "video/mp4",
            (input, output) => _video.ResizeVideoAsync(input, output, width, height, ct), ct);
        return OkVideo(outputKey, $"Resized to {width}x{height}");
    }

    private async Task<NodeExecuteResponse> ExecuteExtractFrameAsync(NodeExecuteRequest request, CancellationToken ct)
    {
        var inputKey = GetInput(request, "video");
        var time = GetParamDouble(request, "timeSec") ?? 0;
        var outputKey = await ProcessMediaAsync(inputKey, "frame", "jpg", "image/jpeg",
            (input, output) => _video.ExtractFrameAsync(input, output, time, ct), ct);
        return new NodeExecuteResponse
        {
            Outputs = new Dictionary<string, string> { ["image"] = outputKey },
            Logs = [$"Frame at {time}s"]
        };
    }

    private async Task<NodeExecuteResponse> ExecuteRotateAsync(NodeExecuteRequest request, CancellationToken ct)
    {
        var inputKey = GetInput(request, "video");
        var degrees = GetParamInt(request, "degrees") ?? 90;
        if (degrees is not (90 or 180 or 270))
            throw new InvalidOperationException("degrees must be 90, 180, or 270.");
        var outputKey = await ProcessMediaAsync(inputKey, "rot", "mp4", "video/mp4",
            (input, output) => _video.RotateVideoAsync(input, output, degrees, ct), ct);
        return OkVideo(outputKey, $"Rotated {degrees}°");
    }

    private async Task<NodeExecuteResponse> ExecuteVolumeAsync(NodeExecuteRequest request, CancellationToken ct)
    {
        var inputKey = GetInput(request, "video");
        var volume = GetParamDouble(request, "volume") ?? 1;
        var outputKey = await ProcessMediaAsync(inputKey, "vol", "mp4", "video/mp4",
            (input, output) => _video.SetVolumeAsync(input, output, volume, ct), ct);
        return OkVideo(outputKey, $"Volume set to {volume}");
    }

    private async Task<NodeExecuteResponse> ExecuteResizeImageAsync(NodeExecuteRequest request, CancellationToken ct)
    {
        var inputKey = GetInput(request, "image");
        var width = GetParamInt(request, "width") ?? 1024;
        var height = GetParamInt(request, "height") ?? 1024;
        var outputKey = await ProcessMediaAsync(inputKey, "iscale", "png", "image/png",
            (input, output) => _video.ResizeImageAsync(input, output, width, height, ct), ct);
        return OkImage(outputKey, $"Resized to {width}x{height}");
    }

    private async Task<NodeExecuteResponse> ExecuteCropImageAsync(NodeExecuteRequest request, CancellationToken ct)
    {
        var inputKey = GetInput(request, "image");
        var x = GetParamInt(request, "x") ?? 0;
        var y = GetParamInt(request, "y") ?? 0;
        var width = GetParamInt(request, "width") ?? 512;
        var height = GetParamInt(request, "height") ?? 512;
        var outputKey = await ProcessMediaAsync(inputKey, "crop", "png", "image/png",
            (input, output) => _video.CropImageAsync(input, output, x, y, width, height, ct), ct);
        return OkImage(outputKey, $"Cropped {width}x{height} at {x},{y}");
    }

    private async Task<NodeExecuteResponse> ExecuteBlurImageAsync(NodeExecuteRequest request, CancellationToken ct)
    {
        var inputKey = GetInput(request, "image");
        var sigma = GetParamDouble(request, "sigma") ?? 5;
        var outputKey = await ProcessMediaAsync(inputKey, "blur", "png", "image/png",
            (input, output) => _video.BlurImageAsync(input, output, sigma, ct), ct);
        return OkImage(outputKey, $"Blur sigma={sigma}");
    }

    private async Task<NodeExecuteResponse> ExecuteImageToVideoAsync(NodeExecuteRequest request, CancellationToken ct)
    {
        var inputKey = GetInput(request, "image");
        var duration = GetParamDouble(request, "durationSec") ?? 3;
        var fps = GetParamInt(request, "fps") ?? 30;
        var outputKey = await ProcessMediaAsync(inputKey, "img2vid", "mp4", "video/mp4",
            (input, output) => _video.ImageToVideoAsync(input, output, duration, fps, ct), ct);
        return OkVideo(outputKey, $"Image to video {duration}s @ {fps}fps");
    }

    private async Task<NodeExecuteResponse> ExecuteCropVideoAsync(NodeExecuteRequest request, CancellationToken ct)
    {
        var inputKey = GetInput(request, "video");
        var x = GetParamInt(request, "x") ?? 0;
        var y = GetParamInt(request, "y") ?? 0;
        var width = GetParamInt(request, "width") ?? 512;
        var height = GetParamInt(request, "height") ?? 512;
        var outputKey = await ProcessMediaAsync(inputKey, "vcrop", "mp4", "video/mp4",
            (input, output) => _video.CropVideoAsync(input, output, x, y, width, height, ct), ct);
        return OkVideo(outputKey, $"Cropped video {width}x{height} at {x},{y}");
    }

    private async Task<NodeExecuteResponse> ExecuteFlipVideoAsync(NodeExecuteRequest request, CancellationToken ct)
    {
        var inputKey = GetInput(request, "video");
        var horizontal = IsHorizontalFlip(request);
        var outputKey = await ProcessMediaAsync(inputKey, "vflip", "mp4", "video/mp4",
            (input, output) => _video.FlipVideoAsync(input, output, horizontal, ct), ct);
        return OkVideo(outputKey, horizontal ? "Flipped horizontally" : "Flipped vertically");
    }

    private async Task<NodeExecuteResponse> ExecuteReverseVideoAsync(NodeExecuteRequest request, CancellationToken ct)
    {
        var inputKey = GetInput(request, "video");
        var outputKey = await ProcessMediaAsync(inputKey, "vrev", "mp4", "video/mp4",
            (input, output) => _video.ReverseVideoAsync(input, output, ct), ct);
        return OkVideo(outputKey, "Video reversed");
    }

    private async Task<NodeExecuteResponse> ExecuteRotateImageAsync(NodeExecuteRequest request, CancellationToken ct)
    {
        var inputKey = GetInput(request, "image");
        var degrees = GetParamInt(request, "degrees") ?? 90;
        if (degrees is not (90 or 180 or 270))
            throw new InvalidOperationException("degrees must be 90, 180, or 270.");
        var outputKey = await ProcessMediaAsync(inputKey, "irot", "png", "image/png",
            (input, output) => _video.RotateImageAsync(input, output, degrees, ct), ct);
        return OkImage(outputKey, $"Rotated {degrees}°");
    }

    private async Task<NodeExecuteResponse> ExecuteFlipImageAsync(NodeExecuteRequest request, CancellationToken ct)
    {
        var inputKey = GetInput(request, "image");
        var horizontal = IsHorizontalFlip(request);
        var outputKey = await ProcessMediaAsync(inputKey, "iflip", "png", "image/png",
            (input, output) => _video.FlipImageAsync(input, output, horizontal, ct), ct);
        return OkImage(outputKey, horizontal ? "Flipped horizontally" : "Flipped vertically");
    }

    private async Task<NodeExecuteResponse> ExecuteGrayscaleImageAsync(NodeExecuteRequest request, CancellationToken ct)
    {
        var inputKey = GetInput(request, "image");
        var outputKey = await ProcessMediaAsync(inputKey, "gray", "png", "image/png",
            (input, output) => _video.GrayscaleImageAsync(input, output, ct), ct);
        return OkImage(outputKey, "Converted to grayscale");
    }

    private async Task<NodeExecuteResponse> ExecuteTrimAudioAsync(NodeExecuteRequest request, CancellationToken ct)
    {
        var inputKey = GetInput(request, "audio");
        var start = GetParamDouble(request, "startSec") ?? 0;
        var duration = GetParamDouble(request, "durationSec");
        var outputKey = await ProcessMediaAsync(inputKey, "atrim", "mp3", "audio/mpeg",
            (input, output) => _video.TrimAudioAsync(input, output, start, duration, ct), ct);
        return OkAudio(outputKey, $"Trimmed audio from {start}s");
    }

    private async Task<NodeExecuteResponse> ExecuteAudioVolumeAsync(NodeExecuteRequest request, CancellationToken ct)
    {
        var inputKey = GetInput(request, "audio");
        var volume = GetParamDouble(request, "volume") ?? 1;
        var outputKey = await ProcessMediaAsync(inputKey, "avol", "mp3", "audio/mpeg",
            (input, output) => _video.SetAudioVolumeAsync(input, output, volume, ct), ct);
        return OkAudio(outputKey, $"Audio volume set to {volume}");
    }

    private async Task<NodeExecuteResponse> ExecuteAudioSpeedAsync(NodeExecuteRequest request, CancellationToken ct)
    {
        var inputKey = GetInput(request, "audio");
        var speed = GetParamDouble(request, "speed") ?? 1.5;
        var outputKey = await ProcessMediaAsync(inputKey, "aspd", "mp3", "audio/mpeg",
            (input, output) => _video.ChangeAudioSpeedAsync(input, output, speed, ct), ct);
        return OkAudio(outputKey, $"Audio speed {speed}x");
    }

    private async Task<NodeExecuteResponse> ExecuteFadeAudioAsync(NodeExecuteRequest request, CancellationToken ct)
    {
        var inputKey = GetInput(request, "audio");
        var fadeIn = GetParamDouble(request, "fadeInSec") ?? 1;
        var fadeOut = GetParamDouble(request, "fadeOutSec") ?? 1;
        var outputKey = await ProcessMediaAsync(inputKey, "afade", "mp3", "audio/mpeg",
            (input, output) => _video.FadeAudioAsync(input, output, fadeIn, fadeOut, ct), ct);
        return OkAudio(outputKey, $"Fade in={fadeIn}s out={fadeOut}s");
    }

    private async Task<NodeExecuteResponse> ExecuteReverseAudioAsync(NodeExecuteRequest request, CancellationToken ct)
    {
        var inputKey = GetInput(request, "audio");
        var outputKey = await ProcessMediaAsync(inputKey, "arev", "mp3", "audio/mpeg",
            (input, output) => _video.ReverseAudioAsync(input, output, ct), ct);
        return OkAudio(outputKey, "Audio reversed");
    }

    private async Task<NodeExecuteResponse> ExecuteAudioToVideoAsync(NodeExecuteRequest request, CancellationToken ct)
    {
        var inputKey = GetInput(request, "audio");
        var width = GetParamInt(request, "width") ?? 1280;
        var height = GetParamInt(request, "height") ?? 720;
        var outputKey = await ProcessMediaAsync(inputKey, "a2v", "mp4", "video/mp4",
            (input, output) => _video.AudioToVideoAsync(input, output, width, height, ct), ct);
        return OkVideo(outputKey, $"Audio wrapped into {width}x{height} video");
    }

    private static bool IsHorizontalFlip(NodeExecuteRequest request)
    {
        var dir = (GetParamString(request, "direction") ?? "horizontal").Trim().ToLowerInvariant();
        return dir is not ("vertical" or "v" or "y");
    }

    private static NodeExecuteResponse OkVideo(string key, string log) => new()
    {
        Outputs = new Dictionary<string, string> { ["video"] = key },
        Logs = [log]
    };

    private static NodeExecuteResponse OkImage(string key, string log) => new()
    {
        Outputs = new Dictionary<string, string> { ["image"] = key },
        Logs = [log]
    };

    private static NodeExecuteResponse OkAudio(string key, string log) => new()
    {
        Outputs = new Dictionary<string, string> { ["audio"] = key },
        Logs = [log]
    };

    private async Task<string> ProcessMediaAsync(
        string inputKey,
        string suffix,
        string extension,
        string contentType,
        Func<string, string, Task> process,
        CancellationToken ct)
    {
        var workId = Guid.NewGuid().ToString("N");
        var inputPath = Path.Combine(_ffmpegOptions.TempDirectory, $"{workId}_in");
        var outputPath = Path.Combine(_ffmpegOptions.TempDirectory, $"{workId}_{suffix}.{extension}");

        try
        {
            await using (var download = await _storage.DownloadAsync(inputKey, ct))
            await using (var fs = File.Create(inputPath))
            {
                await download.CopyToAsync(fs, ct);
            }

            await process(inputPath, outputPath);

            await using var upload = File.OpenRead(outputPath);
            var outputKey = await _storage.UploadAsync(upload, contentType, preferredKey: null, ct);
            _logger.LogInformation("Processed {Input} -> {Output} ({ContentType})", inputKey, outputKey, contentType);
            return outputKey;
        }
        finally
        {
            TryDelete(inputPath);
            TryDelete(outputPath);
        }
    }

    private static string GetInput(NodeExecuteRequest request, string name)
    {
        if (!request.Inputs.TryGetValue(name, out var key) || string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException($"Missing input '{name}'.");
        return key;
    }

    private static string? GetParamString(NodeExecuteRequest request, string name)
    {
        if (request.Params is null || !request.Params.TryGetValue(name, out var el))
            return null;
        return el.ValueKind == JsonValueKind.String ? el.GetString() : el.ToString();
    }

    private static double? GetParamDouble(NodeExecuteRequest request, string name)
    {
        if (request.Params is null || !request.Params.TryGetValue(name, out var el))
            return null;
        return el.ValueKind switch
        {
            JsonValueKind.Number => el.GetDouble(),
            JsonValueKind.String when double.TryParse(el.GetString(), out var v) => v,
            _ => null
        };
    }

    private static int? GetParamInt(NodeExecuteRequest request, string name)
    {
        var d = GetParamDouble(request, name);
        return d is null ? null : (int)Math.Round(d.Value);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
            // ignore cleanup errors
        }
    }
}
