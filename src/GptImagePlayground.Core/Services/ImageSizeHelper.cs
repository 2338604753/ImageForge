using System.Text.RegularExpressions;

namespace GptImagePlayground.Core.Services;

/// <summary>图片尺寸规格化（对齐官方 gpt-image 约束）。</summary>
public static class ImageSizeHelper
{
    private const int SIZE_MULTIPLE = 16;
    private const int MAX_EDGE = 3840;
    private const double MAX_ASPECT_RATIO = 3.0;
    private const int MIN_PIXELS = 655360;
    private const int MAX_PIXELS = 8294400;
    private const int MAX_1K_PIXELS = 1572864;

    private static readonly Regex SizePattern = new(@"^\s*(\d+)\s*[xX×]\s*(\d+)\s*$", RegexOptions.Compiled);
    private static readonly Regex RatioPattern = new(@"^\s*(\d+(?:\.\d+)?)\s*[:xX×]\s*(\d+(?:\.\d+)?)\s*$", RegexOptions.Compiled);

    public static int RoundToMultiple(int value, int multiple)
    {
        return Math.Max(multiple, (int)Math.Round(value / (double)multiple, MidpointRounding.AwayFromZero) * multiple);
    }

    private static int FloorToMultiple(int value, int multiple) => Math.Max(multiple, (int)Math.Floor(value / (double)multiple) * multiple);
    private static int CeilToMultiple(int value, int multiple) => Math.Max(multiple, (int)Math.Ceiling(value / (double)multiple) * multiple);

    private static (int width, int height) NormalizeDimensions(int width, int height)
    {
        var w = RoundToMultiple(width, SIZE_MULTIPLE);
        var h = RoundToMultiple(height, SIZE_MULTIPLE);

        void ScaleToFit(double scale)
        {
            w = FloorToMultiple((int)(w * scale), SIZE_MULTIPLE);
            h = FloorToMultiple((int)(h * scale), SIZE_MULTIPLE);
        }

        void ScaleToFill(double scale)
        {
            w = CeilToMultiple((int)(w * scale), SIZE_MULTIPLE);
            h = CeilToMultiple((int)(h * scale), SIZE_MULTIPLE);
        }

        for (var i = 0; i < 4; i++)
        {
            var maxEdge = Math.Max(w, h);
            if (maxEdge > MAX_EDGE) ScaleToFit((double)MAX_EDGE / maxEdge);

            if (w / (double)h > MAX_ASPECT_RATIO) w = FloorToMultiple((int)(h * MAX_ASPECT_RATIO), SIZE_MULTIPLE);
            else if (h / (double)w > MAX_ASPECT_RATIO) h = FloorToMultiple((int)(w * MAX_ASPECT_RATIO), SIZE_MULTIPLE);

            var pixels = (long)w * h;
            if (pixels > MAX_PIXELS) ScaleToFit(Math.Sqrt((double)MAX_PIXELS / pixels));
            else if (pixels < MIN_PIXELS) ScaleToFill(Math.Sqrt((double)MIN_PIXELS / pixels));
        }

        return (w, h);
    }

    public static string NormalizeImageSize(string size)
    {
        var trimmed = size.Trim();
        var match = SizePattern.Match(trimmed);
        if (!match.Success) return trimmed;

        var (w, h) = NormalizeDimensions(int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value));
        return $"{w}x{h}";
    }

    public static bool TryParseRatio(string ratio, out double width, out double height)
    {
        width = 0; height = 0;
        var match = RatioPattern.Match(ratio);
        if (!match.Success) return false;
        width = double.Parse(match.Groups[1].Value);
        height = double.Parse(match.Groups[2].Value);
        return width > 0 && height > 0;
    }

    /// <summary>1K/2K/4K 档位像素预算。</summary>
    private static readonly Dictionary<string, int> TierPixelBudget = new()
    {
        ["1K"] = MAX_1K_PIXELS,
        ["2K"] = 4194304,
        ["4K"] = MAX_PIXELS,
    };

    private static readonly Dictionary<string, Dictionary<string, string>> CommonSizePresets = new()
    {
        ["1K"] = new() { ["1:1"] = "1024x1024", ["3:2"] = "1536x1024", ["2:3"] = "1024x1536", ["16:9"] = "1280x720", ["9:16"] = "720x1280", ["4:3"] = "1024x768", ["3:4"] = "768x1024", ["21:9"] = "1280x544" },
        ["2K"] = new() { ["1:1"] = "2048x2048", ["3:2"] = "2160x1440", ["2:3"] = "1440x2160", ["16:9"] = "2560x1440", ["9:16"] = "1440x2560", ["4:3"] = "2048x1536", ["3:4"] = "1536x2048", ["21:9"] = "2560x1088" },
        ["4K"] = new() { ["1:1"] = "2880x2880", ["3:2"] = "3456x2304", ["2:3"] = "2304x3456", ["16:9"] = "3840x2160", ["9:16"] = "2160x3840", ["4:3"] = "3200x2400", ["3:4"] = "2400x3200", ["21:9"] = "3840x1600" },
    };

    /// <summary>根据档位和比例计算最优尺寸。</summary>
    public static string? CalculateImageSize(string tier, string ratio)
    {
        if (!TryParseRatio(ratio, out var rw, out var rh)) return null;
        var key = $"{ToInt(rw)}:{ToInt(rh)}";
        if (CommonSizePresets[tier].TryGetValue(key, out var preset)) return preset;

        var targetRatio = rw / rh;
        var budget = TierPixelBudget[tier];
        var best = (width: 0, height: 0, pixels: 0L);

        for (var w = SIZE_MULTIPLE; w <= MAX_EDGE; w += SIZE_MULTIPLE)
        {
            var idealH = w / targetRatio;
            var candidates = new[] { FloorToMultiple((int)idealH, SIZE_MULTIPLE), CeilToMultiple((int)idealH, SIZE_MULTIPLE) };
            foreach (var h in candidates)
            {
                if (h < SIZE_MULTIPLE || h > MAX_EDGE) continue;
                var pixels = (long)w * h;
                if (pixels > budget || pixels < MIN_PIXELS) continue;
                if (Math.Max(w / (double)h, h / (double)w) > MAX_ASPECT_RATIO) continue;
                var ratioError = Math.Abs(w / (double)h - targetRatio) / targetRatio;
                if (ratioError > 0.01) continue;
                if (pixels > best.pixels) best = (w, h, pixels);
            }
        }

        return best.pixels == 0 ? null : $"{best.width}x{best.height}";
    }

    private static int ToInt(double value) => (int)Math.Round(value);
}
