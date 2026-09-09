using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Security.Cryptography;
using System.Text;
using GptImagePlayground.Core.Models;

namespace GptImagePlayground.Core.Services;

/// <summary>
/// 图片存储：以 SHA-256 去重命名保存文件，生成缩略图。
/// </summary>
public class ImageStore
{
    private readonly SettingsService _settings;
    private readonly object _lock = new();

    public ImageStore(SettingsService settings)
    {
        _settings = settings;
    }

    private string DataDir => _settings.DataDirectory;
    private string ImagesDir => AppPaths.ImagesDirectory(DataDir);
    private string ThumbsDir => AppPaths.ThumbnailsDirectory(DataDir);

    public string ComputeId(byte[] bytes)
    {
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public StoredImage SaveImage(byte[] bytes, ImageSource source, string? mime = null)
    {
        var id = ComputeId(bytes);
        var ext = GuessExtension(mime ?? MimeFromBytes(bytes));
        var fileName = $"{id}.{ext}";
        var fullPath = Path.Combine(ImagesDir, fileName);

        lock (_lock)
        {
            AppPaths.EnsureDirectories(DataDir);
            if (!File.Exists(fullPath))
            {
                File.WriteAllBytes(fullPath, bytes);
                // 生成缩略图
                try
                {
                    CreateThumbnail(fullPath, Path.Combine(ThumbsDir, $"{id}.thumb.png"));
                }
                catch
                {
                    // 缩略图失败不致命
                }
            }
        }

        var (w, h) = GetDimensions(fullPath);
        return new StoredImage
        {
            Id = id,
            FilePath = Path.GetRelativePath(DataDir, fullPath),
            Mime = mime ?? MimeFromBytes(bytes),
            Source = source,
            CreatedAt = DateTime.UtcNow,
            Width = w,
            Height = h
        };
    }

    public string GetFullPath(StoredImage image) => Path.Combine(DataDir, image.FilePath);
    public string GetFullPath(string relativePath) => Path.GetFullPath(Path.Combine(DataDir, relativePath));

    public string? GetThumbnailPath(string imageId)
    {
        var thumb = Path.Combine(ThumbsDir, $"{imageId}.thumb.png");
        return File.Exists(thumb) ? thumb : null;
    }

    /// <summary>读取图片为字节。</summary>
    public byte[] ReadImage(StoredImage image) => File.ReadAllBytes(GetFullPath(image));

    private void CreateThumbnail(string sourcePath, string thumbPath, int? size = null)
    {
        var target = size ?? Math.Max(64, _settings.Settings.ThumbnailSize);
        using var src = new Bitmap(sourcePath);
        var scale = Math.Min(1.0, (double)target / Math.Max(src.Width, src.Height));
        var w = Math.Max(1, (int)(src.Width * scale));
        var h = Math.Max(1, (int)(src.Height * scale));
        using var dst = new Bitmap(w, h);
        using var g = Graphics.FromImage(dst);
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.DrawImage(src, 0, 0, w, h);
        dst.Save(thumbPath, ImageFormat.Png);
    }

    public (int? w, int? h) GetDimensions(string fullPath)
    {
        try
        {
            using var bmp = new Bitmap(fullPath);
            return (bmp.Width, bmp.Height);
        }
        catch
        {
            return (null, null);
        }
    }

    private static string GuessExtension(string mime)
    {
        return mime switch
        {
            "image/jpeg" => "jpg",
            "image/webp" => "webp",
            "image/gif" => "gif",
            _ => "png"
        };
    }

    private static string MimeFromBytes(byte[] bytes)
    {
        if (bytes.Length > 3 && bytes[0] == 0xFF && bytes[1] == 0xD8) return "image/jpeg";
        if (bytes.Length > 8 && Encoding.ASCII.GetString(bytes, 0, 4) == "RIFF") return "image/webp";
        if (bytes.Length > 3 && Encoding.ASCII.GetString(bytes, 0, 4) == "GIF8") return "image/gif";
        return "image/png";
    }
}
