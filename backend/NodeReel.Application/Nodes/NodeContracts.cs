using System.Text.Json;
using System.Text.Json.Serialization;

namespace NodeReel.Application.Nodes;

public sealed class NodeDescriptor
{
    public required string Id { get; init; }
    public required string ProviderId { get; init; }
    public required string Name { get; init; }
    public string Category { get; init; } = "general";
    public string Description { get; init; } = string.Empty;
    /// <summary>Optional icon key or URL for UI (e.g. upload, strip, noise).</summary>
    public string? Icon { get; init; }
    public string? Subtitle { get; init; }
    public List<NodePort> Inputs { get; init; } = [];
    public List<NodePort> Outputs { get; init; } = [];
    public JsonElement ParamsSchema { get; init; } = NodeSchemaHelper.EmptyObjectSchema();
}

public sealed class NodePort
{
    public required string Name { get; init; }
    public required string Type { get; init; }
    public bool Required { get; init; } = true;
}

public sealed class NodeExecuteRequest
{
    public required string NodeId { get; init; }
    public Dictionary<string, JsonElement>? Params { get; init; }
    public Dictionary<string, string> Inputs { get; init; } = new();
}

public sealed class NodeExecuteResponse
{
    public Dictionary<string, string> Outputs { get; init; } = new();
    public List<string>? Logs { get; init; }
}

public static class LocalNodeIds
{
    public const string ProviderId = "local";

    public const string UploadVideo = "upload-video";
    public const string UploadImage = "upload-image";
    public const string UploadAudio = "upload-audio";
    public const string DownloadSocialVideo = "download-social-video";

    public const string StripMetadata = "strip-metadata";
    public const string AddInvisibleNoise = "add-invisible-noise";
    public const string TrimVideo = "trim-video";
    public const string ExtractAudio = "extract-audio";
    public const string RemoveAudio = "remove-audio";
    public const string ChangeSpeed = "change-speed";
    public const string ResizeVideo = "resize-video";
    public const string ExtractFrame = "extract-frame";
    public const string RotateVideo = "rotate-video";
    public const string SetVolume = "set-volume";
    public const string CropVideo = "crop-video";
    public const string FlipVideo = "flip-video";
    public const string ReverseVideo = "reverse-video";

    public const string ResizeImage = "resize-image";
    public const string CropImage = "crop-image";
    public const string BlurImage = "blur-image";
    public const string ImageToVideo = "image-to-video";
    public const string RotateImage = "rotate-image";
    public const string FlipImage = "flip-image";
    public const string GrayscaleImage = "grayscale-image";

    public const string TrimAudio = "trim-audio";
    public const string ChangeAudioVolume = "change-audio-volume";
    public const string ChangeAudioSpeed = "change-audio-speed";
    public const string FadeAudio = "fade-audio";
    public const string ReverseAudio = "reverse-audio";
    public const string AudioToVideo = "audio-to-video";

    public const string If = "if";
    public const string Switch = "switch";
    public const string Merge = "merge";
}

public static class NodeSchemaHelper
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static JsonElement EmptyObjectSchema() =>
        JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new { }
        }, Options);

    public static JsonElement NoiseParamsSchema() =>
        JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new
            {
                strength = new
                {
                    type = "number",
                    minimum = 0.1,
                    maximum = 20,
                    @default = 2,
                    description = "Noise strength"
                }
            }
        }, Options);

    public static JsonElement UploadParamsSchema(string mediaKind = "video") =>
        JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new
            {
                objectKey = new
                {
                    type = "string",
                    description = $"Object key of an already uploaded {mediaKind}"
                }
            },
            required = new[] { "objectKey" }
        }, Options);

    public static JsonElement TrimParamsSchema() =>
        JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new
            {
                startSec = new { type = "number", minimum = 0, @default = 0, description = "Start time in seconds" },
                durationSec = new { type = "number", minimum = 0.1, description = "Clip length in seconds (optional)" }
            }
        }, Options);

    public static JsonElement SpeedParamsSchema() =>
        JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new
            {
                speed = new
                {
                    type = "number",
                    minimum = 0.25,
                    maximum = 4,
                    @default = 1.5,
                    description = "Playback speed multiplier"
                }
            }
        }, Options);

    public static JsonElement SizeParamsSchema(int defaultWidth, int defaultHeight) =>
        JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new
            {
                width = new { type = "number", minimum = 1, @default = defaultWidth, description = "Target width (px)" },
                height = new { type = "number", minimum = 1, @default = defaultHeight, description = "Target height (px)" }
            },
            required = new[] { "width", "height" }
        }, Options);

    public static JsonElement FrameParamsSchema() =>
        JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new
            {
                timeSec = new { type = "number", minimum = 0, @default = 0, description = "Timestamp of the frame (seconds)" }
            }
        }, Options);

    public static JsonElement RotateParamsSchema() =>
        JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new
            {
                degrees = new
                {
                    type = "number",
                    @default = 90,
                    description = "Rotation: 90, 180, or 270"
                }
            }
        }, Options);

    public static JsonElement VolumeParamsSchema() =>
        JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new
            {
                volume = new
                {
                    type = "number",
                    minimum = 0,
                    maximum = 4,
                    @default = 1,
                    description = "Linear volume (1 = unchanged, 0 = mute)"
                }
            }
        }, Options);

    public static JsonElement CropParamsSchema() =>
        JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new
            {
                x = new { type = "number", minimum = 0, @default = 0, description = "Crop X offset" },
                y = new { type = "number", minimum = 0, @default = 0, description = "Crop Y offset" },
                width = new { type = "number", minimum = 1, @default = 512, description = "Crop width" },
                height = new { type = "number", minimum = 1, @default = 512, description = "Crop height" }
            },
            required = new[] { "width", "height" }
        }, Options);

    public static JsonElement BlurParamsSchema() =>
        JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new
            {
                sigma = new
                {
                    type = "number",
                    minimum = 0.1,
                    maximum = 50,
                    @default = 5,
                    description = "Gaussian blur sigma"
                }
            }
        }, Options);

    public static JsonElement ImageToVideoParamsSchema() =>
        JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new
            {
                durationSec = new { type = "number", minimum = 0.1, maximum = 600, @default = 3, description = "Video duration (seconds)" },
                fps = new { type = "number", minimum = 1, maximum = 60, @default = 30, description = "Frames per second" }
            }
        }, Options);

    public static JsonElement IfParamsSchema() =>
        JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new
            {
                condition = new
                {
                    type = "string",
                    description = "true | false | notEmpty",
                    @default = "true"
                }
            }
        }, Options);

    public static JsonElement SwitchParamsSchema() =>
        JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new
            {
                route = new
                {
                    type = "string",
                    description = "Which output to take: 0, 1, 2, or default",
                    @default = "0"
                }
            }
        }, Options);

    public static JsonElement FlipParamsSchema() =>
        JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new
            {
                direction = new
                {
                    type = "string",
                    @enum = new[] { "horizontal", "vertical" },
                    @default = "horizontal",
                    description = "Mirror horizontally or vertically"
                }
            }
        }, Options);

    public static JsonElement SocialDownloadParamsSchema() =>
        JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new
            {
                platform = new
                {
                    type = "string",
                    @enum = new[] { "youtube", "tiktok", "instagram" },
                    @default = "youtube",
                    description = "Source platform"
                },
                url = new
                {
                    type = "string",
                    description = "Public video URL"
                }
            },
            required = new[] { "url" }
        }, Options);

    public static JsonElement FadeParamsSchema() =>
        JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new
            {
                fadeInSec = new { type = "number", minimum = 0, maximum = 60, @default = 1, description = "Fade-in duration (seconds)" },
                fadeOutSec = new { type = "number", minimum = 0, maximum = 60, @default = 1, description = "Fade-out duration (seconds)" }
            }
        }, Options);

    public static JsonElement AudioToVideoParamsSchema() =>
        JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new
            {
                width = new { type = "number", minimum = 16, @default = 1280, description = "Video width" },
                height = new { type = "number", minimum = 16, @default = 720, description = "Video height" }
            }
        }, Options);
}
