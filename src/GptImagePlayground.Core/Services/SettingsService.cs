using System.Text.Json;
using System.Text.Json.Serialization;
using GptImagePlayground.Core.Models;

namespace GptImagePlayground.Core.Services;

/// <summary>应用设置读写（settings.json）。</summary>
public class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly object _lock = new();
    private string _dataDir = "";
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public string DataDirectory => _dataDir;

    public event EventHandler<string>? DataDirectoryChanged;

    public AppSettings Settings { get; private set; } = new();

    /// <summary>初始化加载设置；会根据传入目录或默认目录读取。</summary>
    public void Load(string? dataDir = null)
    {
        _dataDir = string.IsNullOrWhiteSpace(dataDir) ? AppPaths.DefaultDataDirectory : dataDir;
        AppPaths.EnsureDirectories(_dataDir);
        _semaphore.Wait();
        try
        {
            var file = AppPaths.SettingsFile(_dataDir);
            if (File.Exists(file))
            {
                var json = File.ReadAllText(file);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
                Settings = loaded;
            }
            else
            {
                Settings = new AppSettings();
            }

            // 确保有至少一个可用配置
            if (Settings.Profiles.Count == 0)
            {
                Settings.Profiles.Add(new ApiProfile
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Name = "OpenAI 兼容",
                    Provider = "openai",
                    BaseUrl = "https://api.openai.com/v1",
                    Model = "gpt-image-2.5-sunburst",
                    ApiMode = ApiMode.Images,
                    Timeout = 600
                });
                Settings.ActiveProfileId = Settings.Profiles[0].Id;
            }
            else if (Settings.ActiveProfileId is null or "" || Settings.Profiles.All(p => p.Id != Settings.ActiveProfileId))
            {
                Settings.ActiveProfileId = Settings.Profiles[0].Id;
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>切换数据目录（用户自选），迁移旧数据。</summary>
    public bool ChangeDataDirectory(string newDir)
    {
        if (string.IsNullOrWhiteSpace(newDir)) return false;
        newDir = Path.GetFullPath(newDir);
        if (newDir == _dataDir) return true;

        AppPaths.EnsureDirectories(newDir);

        // 迁移 settings
        var oldSettingsFile = AppPaths.SettingsFile(_dataDir);
        if (File.Exists(oldSettingsFile))
        {
            File.Copy(oldSettingsFile, AppPaths.SettingsFile(newDir), overwrite: true);
        }
        // 迁移数据库
        var oldDb = AppPaths.DatabaseFile(_dataDir);
        if (File.Exists(oldDb))
        {
            File.Copy(oldDb, AppPaths.DatabaseFile(newDir), overwrite: true);
        }

        _dataDir = newDir;
        Settings.DataDirectory = newDir;
        Save();
        DataDirectoryChanged?.Invoke(this, newDir);
        return true;
    }

    public void Save()
    {
        _semaphore.Wait();
        try
        {
            AppPaths.EnsureDirectories(_dataDir);
            var file = AppPaths.SettingsFile(_dataDir);
            var json = JsonSerializer.Serialize(Settings, JsonOptions);
            File.WriteAllText(file, json);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>获取当前激活的 API 配置（无则首个）。</summary>
    public ApiProfile GetActiveProfile()
    {
        var active = Settings.ActiveProfile;
        if (active != null) return active;
        if (Settings.Profiles.Count > 0)
        {
            Settings.ActiveProfileId = Settings.Profiles[0].Id;
            return Settings.Profiles[0];
        }
        throw new InvalidOperationException("尚未配置任何 API 配置，请打开设置添加。");
    }
}
