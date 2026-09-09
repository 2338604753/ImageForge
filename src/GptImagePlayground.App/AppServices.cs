using GptImagePlayground.Core.Services;

namespace GptImagePlayground.App;

/// <summary>应用服务容器（组合根）。</summary>
public static class AppServices
{
    public static SettingsService Settings { get; private set; } = new();
    public static TaskStore Tasks { get; private set; } = null!;
    public static ImageStore Images { get; private set; } = null!;
    public static ImageApiService Api { get; private set; } = new();

    public static void Initialize(string? dataDir = null)
    {
        Settings.Load(dataDir);
        Tasks = new TaskStore(Settings);
        Images = new ImageStore(Settings);
        if (!string.IsNullOrWhiteSpace(Settings.Settings.DataDirectory))
            Settings.DataDirectoryChanged += (_, dir) => RecreateStores(dir);
    }

    private static void RecreateStores(string dir)
    {
        Tasks = new TaskStore(Settings);
        Images = new ImageStore(Settings);
    }
}
