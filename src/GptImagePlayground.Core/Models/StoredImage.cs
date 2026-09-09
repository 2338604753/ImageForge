namespace GptImagePlayground.Core.Models;

/// <summary>图片来源。</summary>
public enum ImageSource
{
    Upload,
    Generated,
    Mask
}

/// <summary>存储在磁盘上的图片（SHA-256 去重命名）。</summary>
public class StoredImage
{
    public string Id { get; set; } = "";            // SHA-256 hash
    public string FilePath { get; set; } = "";      // 相对数据目录的路径
    public string Mime { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ImageSource Source { get; set; } = ImageSource.Generated;
    public int? Width { get; set; }
    public int? Height { get; set; }
}

/// <summary>图片缩略图（列表展示用，避免解码 4K 原图）。</summary>
public class StoredImageThumbnail
{
    public string Id { get; set; } = "";
    public string ThumbnailFilePath { get; set; } = "";
    public int? Width { get; set; }
    public int? Height { get; set; }
    public int ThumbnailVersion { get; set; } = 1;
}
