namespace GptImagePlayground.Core.Services;

/// <summary>解析应用数据目录。</summary>
public static class AppPaths
{
    public static string DefaultDataDirectory
    {
        get
        {
            var root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "GptImagePlayground");
            return Path.Combine(root, "data");
        }
    }

    public static string ImagesDirectory(string dataDir) => Path.Combine(dataDir, "images");
    public static string ThumbnailsDirectory(string dataDir) => Path.Combine(dataDir, "thumbs");
    public static string SettingsFile(string dataDir) => Path.Combine(dataDir, "settings.json");
    public static string DatabaseFile(string dataDir) => Path.Combine(dataDir, "tasks.db");

    public static void EnsureDirectories(string dataDir)
    {
        Directory.CreateDirectory(dataDir);
        Directory.CreateDirectory(ImagesDirectory(dataDir));
        Directory.CreateDirectory(ThumbnailsDirectory(dataDir));
    }
}
