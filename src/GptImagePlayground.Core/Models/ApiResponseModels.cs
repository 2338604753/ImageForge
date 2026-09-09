using System.Text.Json.Serialization;

namespace GptImagePlayground.Core.Models;

/// <summary>images/generations 单条结果。</summary>
public class ImageResponseItem
{
    [JsonPropertyName("b64_json")]
    public string? B64Json { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("revised_prompt")]
    public string? RevisedPrompt { get; set; }

    [JsonPropertyName("size")]
    public string? Size { get; set; }

    [JsonPropertyName("quality")]
    public string? Quality { get; set; }

    [JsonPropertyName("output_format")]
    public string? OutputFormat { get; set; }

    [JsonPropertyName("output_compression")]
    public int? OutputCompression { get; set; }

    [JsonPropertyName("moderation")]
    public string? Moderation { get; set; }
}

/// <summary>images/generations / images/edits 响应。</summary>
public class ImageApiResponse
{
    [JsonPropertyName("data")]
    public List<ImageResponseItem>? Data { get; set; } = new();

    [JsonPropertyName("size")]
    public string? Size { get; set; }

    [JsonPropertyName("quality")]
    public string? Quality { get; set; }

    [JsonPropertyName("output_format")]
    public string? OutputFormat { get; set; }

    [JsonPropertyName("output_compression")]
    public int? OutputCompression { get; set; }

    [JsonPropertyName("moderation")]
    public string? Moderation { get; set; }

    [JsonPropertyName("n")]
    public int? N { get; set; }
}

/// <summary>Responses API 的 output 项。</summary>
public class ResponsesOutputItem
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("result")]
    [JsonConverter(typeof(ResponsesResultConverter))]
    public string? Result { get; set; }

    [JsonPropertyName("revised_prompt")]
    public string? RevisedPrompt { get; set; }

    [JsonPropertyName("size")]
    public string? Size { get; set; }

    [JsonPropertyName("quality")]
    public string? Quality { get; set; }

    [JsonPropertyName("output_format")]
    public string? OutputFormat { get; set; }

    [JsonPropertyName("output_compression")]
    public int? OutputCompression { get; set; }

    [JsonPropertyName("moderation")]
    public string? Moderation { get; set; }
}

/// <summary>Responses API 响应。</summary>
public class ResponsesApiResponse
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("output")]
    public List<ResponsesOutputItem>? Output { get; set; } = new();
}

/// <summary>fal.ai 输出文件。</summary>
public class FalImageFile
{
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("content_type")]
    public string? ContentType { get; set; }

    [JsonPropertyName("width")]
    public int? Width { get; set; }

    [JsonPropertyName("height")]
    public int? Height { get; set; }

    [JsonPropertyName("b64_json")]
    public string? B64Json { get; set; }
}

public class FalApiResponse
{
    [JsonPropertyName("images")]
    public List<FalImageFile>? Images { get; set; } = new();

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("seed")]
    public int? Seed { get; set; }
}

/// <summary>SSE 事件。</summary>
public class ServerSentEvent
{
    public string Type { get; set; } = "";
    public string Object { get; set; } = "";
    public string? B64Json { get; set; }
    public int? PartialImageIndex { get; set; }
}
