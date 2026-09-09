using System.Text.Json.Serialization;

namespace GptImagePlayground.Core.Models;

/// <summary>应用设置，持久化为 settings.json。</summary>
public class AppSettings
{
    // ===== 旧版单配置字段（兼容导入） =====
    public string BaseUrl { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "";
    public int Timeout { get; set; } = 600;
    public ApiMode ApiMode { get; set; } = ApiMode.Images;
    public bool CodexCli { get; set; }
    public bool ApiProxy { get; set; }
    public bool StreamImages { get; set; }
    public int StreamPartialImages { get; set; } = 1;

    // ===== 自定义供应商 =====
    public List<CustomProviderDefinition> CustomProviders { get; set; } = new();
    public List<string> ProviderOrder { get; set; } = new();

    // ===== 习惯配置 =====
    public bool ClearInputAfterSubmit { get; set; }
    public bool PersistInputOnRestart { get; set; }
    public bool ReuseTaskApiProfileTemporarily { get; set; } = true;
    public bool AlwaysShowRetryButton { get; set; }
    public bool AllowPromptRewrite { get; set; } = true;
    public bool TaskCompletionNotification { get; set; } = true;
    public bool EnterSubmit { get; set; }
    public int AgentMaxToolRounds { get; set; } = 15;
    public bool AgentWebSearch { get; set; }
    public string AgentApiConfigMode { get; set; } = "off";

    // ===== 存储（用户可自选） =====
    public string DataDirectory { get; set; } = "";
    public int ThumbnailSize { get; set; } = 300;

    // ===== API 配置列表 =====
    public List<ApiProfile> Profiles { get; set; } = new();
    public string ActiveProfileId { get; set; } = "";

    // ===== 临时输入（重启保留） =====
    public string LastPrompt { get; set; } = "";

    [JsonIgnore]
    public ApiProfile? ActiveProfile
    {
        get { return Profiles.FirstOrDefault(p => p.Id == ActiveProfileId) ?? Profiles.FirstOrDefault(); }
    }
}
