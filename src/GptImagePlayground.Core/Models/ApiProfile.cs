namespace GptImagePlayground.Core.Models;

/// <summary>API 模式：images = 图片生成接口，responses = 对话接口（含 image_generation 工具）。</summary>
public enum ApiMode
{
    Images,
    Responses
}

/// <summary>内置/自定义供应商类型（openai、sb2api-async、fal 或自定义供应商 id）。</summary>
public enum ApiProviderKind
{
    OpenAi,
    Sb2ApiAsync,
    Fal,
    Custom
}

/// <summary>某个 API 配置条目。</summary>
public class ApiProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>是否为部署方指定的默认预置配置。</summary>
    public bool IsDefault { get; set; }

    public string Name { get; set; } = "";

    /// <summary>配置说明，支持 Markdown 纯文本展示。</summary>
    public string Description { get; set; } = "";

    public string Provider { get; set; } = "openai";

    public string BaseUrl { get; set; } = "";

    public string ApiKey { get; set; } = "";

    public string Model { get; set; } = "gpt-image-2.5-sunburst";

    /// <summary>Responses API 的 image_generation 工具模型，留空用 API 默认值。</summary>
    public string ImageGenerationModel { get; set; } = "";

    public int Timeout { get; set; } = 600;

    public ApiMode ApiMode { get; set; } = ApiMode.Images;

    /// <summary>Responses API 推理强度。</summary>
    public string ReasoningEffort { get; set; } = "";

    /// <summary>是否启用 Codex CLI 兼容模式。</summary>
    public bool CodexCli { get; set; }

    /// <summary>是否走部署端 API 代理。</summary>
    public bool ApiProxy { get; set; }

    /// <summary>是否要求返回 Base64 图片数据。</summary>
    public bool ResponseFormatB64Json { get; set; }

    /// <summary>是否启用流式传输。</summary>
    public bool StreamImages { get; set; }

    /// <summary>流式中间步骤图片数。</summary>
    public int StreamPartialImages { get; set; } = 1;

    /// <summary>透明背景实现方式：api（原生）或 local（本地后处理）。</summary>
    public string TransparentBackgroundMethod { get; set; } = "api";

    [System.Text.Json.Serialization.JsonIgnore]
    public ApiProviderKind ProviderKind
    {
        get
        {
            return Provider switch
            {
                "openai" => ApiProviderKind.OpenAi,
                "sb2api-async" => ApiProviderKind.Sb2ApiAsync,
                "fal" => ApiProviderKind.Fal,
                _ => ApiProviderKind.Custom
            };
        }
    }

    public ApiProfile Clone()
    {
        return (ApiProfile)MemberwiseClone();
    }
}
