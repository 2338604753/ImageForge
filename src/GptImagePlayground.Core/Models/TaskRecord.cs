namespace GptImagePlayground.Core.Models;

/// <summary>任务生成参数。</summary>
public class TaskParams
{
    public string Size { get; set; } = "auto";
    public string Quality { get; set; } = "auto";
    public string OutputFormat { get; set; } = "png";
    public int? OutputCompression { get; set; }
    public string Moderation { get; set; } = "auto";
    public int N { get; set; } = 1;
    public bool TransparentOutput { get; set; }
}

/// <summary>任务状态。</summary>
public enum TaskStatus
{
    Running,
    Done,
    Error
}

/// <summary>并发多图中失败的单张请求。</summary>
public class OutputError
{
    public int RequestIndex { get; set; }
    public string Error { get; set; } = "";
}

/// <summary>任务记录。</summary>
public class TaskRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Prompt { get; set; } = "";
    public TaskParams Params { get; set; } = new();

    public string ApiProvider { get; set; } = "";
    public string ApiProfileId { get; set; } = "";
    public string ApiProfileName { get; set; } = "";
    public ApiMode ApiMode { get; set; } = ApiMode.Images;
    public string ApiModel { get; set; } = "";

    /// <summary>fal.ai 队列请求 ID（预留）。</summary>
    public string FalRequestId { get; set; } = "";
    public string FalEndpoint { get; set; } = "";

    /// <summary>自定义异步服务商任务 ID（预留）。</summary>
    public string CustomTaskId { get; set; } = "";

    /// <summary>API 返回的实际生效参数（序列化为 JSON 存储）。</summary>
    public TaskParams ActualParams { get; set; } = new();
    public bool HasActualParams { get; set; }

    /// <summary>输出图片对应的 API 改写提示词，key 为输出图片 id。</summary>
    public Dictionary<string, string> RevisedPromptByImage { get; set; } = new();

    public bool TransparentOutput { get; set; }
    public string TransparentPrompt { get; set; } = "";

    /// <summary>透明背景后处理前的原始输出图片 id，顺序对应 OutputImages。</summary>
    public List<string> TransparentOriginalImages { get; set; } = new();

    public List<string> InputImageIds { get; set; } = new();
    public string MaskTargetImageId { get; set; } = "";
    public string MaskImageId { get; set; } = "";

    public List<string> OutputImages { get; set; } = new();

    public List<OutputError> OutputErrors { get; set; } = new();

    /// <summary>流式生成的中间步骤图片（失败时保留）。</summary>
    public List<string> StreamPartialImageIds { get; set; } = new();

    public List<string> RawImageUrls { get; set; } = new();
    public string RawResponsePayload { get; set; } = "";

    public TaskStatus Status { get; set; } = TaskStatus.Running;
    public string Error { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? FinishedAt { get; set; }

    /// <summary>总耗时毫秒。</summary>
    public long? Elapsed { get; set; }

    public bool IsFavorite { get; set; }
    public List<string> FavoriteCollectionIds { get; set; } = new();
    public string SourceMode { get; set; } = "gallery";

    // Agent 相关（预留）
    public string AgentConversationId { get; set; } = "";
    public string AgentRoundId { get; set; } = "";
    public string AgentMessageId { get; set; } = "";
}
