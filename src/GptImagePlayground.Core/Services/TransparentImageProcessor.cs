using System.Drawing;
using System.Drawing.Imaging;

namespace GptImagePlayground.Core.Services;

/// <summary>
/// 透明背景本地后处理（键色抠图）。从原版 transparentImage.ts 移植。
/// </summary>
public static class TransparentImageProcessor
{
    public static readonly Color GreenKey = Color.FromArgb(0, 255, 0);
    public static readonly Color MagentaKey = Color.FromArgb(255, 0, 255);

    public static string BuildTransparentPrompt(string prompt)
    {
        return $"{prompt.Trim()}\n\n" +
            "[背景指令]\n" +
            "背景色选择规则：如果主体包含绿色系（绿、青绿、黄绿、草绿等）颜色，使用纯洋红色(#FF00FF)背景；否则一律使用纯绿色(#00FF00)背景。\n" +
            "背景要求：整张画布仅由所选纯色填充，无任何渐变、纹理、阴影、光照变化、地面或环境元素。\n" +
            "主体要求：单主体、完整呈现、轮廓清晰锐利。主体与背景之间保持干净的边缘分离，不要有颜色溢出或混合。\n" +
            "禁止：主体本身、描边、光晕、投影或反射中不能出现所选背景色。";
    }

    public static List<string> TransparentPromptLines => BuildTransparentPrompt("").Split('\n').Select(x => x.Trim()).Where(x => x.Length > 0).ToList();

    /// <summary>执行键色抠图，返回带透明通道的位图。</summary>
    public static Bitmap RemoveKeyedBackground(Bitmap source, Color? keyColor = null)
    {
        var rgb = keyColor ?? DetectKeyColor(source);
        var width = source.Width;
        var height = source.Height;
        var pixelCount = width * height;

        var data = LockToBgra(source);
        var mask = BuildBackgroundMask(data, width, height, rgb);
        WriteTransparentPixels(data, mask, width, height, rgb);
        var result = UnlockFromBgra(data, width, height);
        return result;
    }

    private static byte[] LockToBgra(Bitmap bmp)
    {
        var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
        var bmpData = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        var bytes = Math.Abs(bmpData.Stride) * bmp.Height;
        var buffer = new byte[bytes];
        System.Runtime.InteropServices.Marshal.Copy(bmpData.Scan0, buffer, 0, bytes);
        bmp.UnlockBits(bmpData);
        return buffer;
    }

    private static Bitmap UnlockFromBgra(byte[] buffer, int width, int height)
    {
        var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        var rect = new Rectangle(0, 0, width, height);
        var bmpData = bmp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        System.Runtime.InteropServices.Marshal.Copy(buffer, 0, bmpData.Scan0, buffer.Length);
        bmp.UnlockBits(bmpData);
        return bmp;
    }

    private static Color DetectKeyColor(Bitmap source)
    {
        var data = LockToBgra(source);
        var width = source.Width;
        var height = source.Height;
        var index = (int x, int y) => y * width + x;
        var greenScore = 0;
        var magentaScore = 0;

        for (var x = 0; x < width; x++)
        {
            ScoreBorder(data, index(x, 0), GreenKey, MagentaKey, ref greenScore, ref magentaScore);
            ScoreBorder(data, index(x, height - 1), GreenKey, MagentaKey, ref greenScore, ref magentaScore);
        }
        for (var y = 1; y < height - 1; y++)
        {
            ScoreBorder(data, index(0, y), GreenKey, MagentaKey, ref greenScore, ref magentaScore);
            ScoreBorder(data, index(width - 1, y), GreenKey, MagentaKey, ref greenScore, ref magentaScore);
        }
        return magentaScore > greenScore ? MagentaKey : GreenKey;
    }

    private static void ScoreBorder(byte[] data, int index, Color green, Color magenta, ref int greenScore, ref int magentaScore)
    {
        var o = index * 4;
        var r = data[o + 2]; var g = data[o + 1]; var b = data[o]; // BGRA
        if (Dist(r, g, b, green.R, green.G, green.B) < 100) greenScore++;
        if (Dist(r, g, b, magenta.R, magenta.G, magenta.B) < 100) magentaScore++;
    }

    private static double Dist(int r1, int g1, int b1, int r2, int g2, int b2)
    {
        var dr = r1 - r2; var dg = g1 - g2; var db = b1 - b2;
        return Math.Sqrt(dr * dr + dg * dg + db * db);
    }

    private static byte[] BuildBackgroundMask(byte[] data, int width, int height, Color key)
    {
        var mask = BuildConnectedBackgroundMask(data, width, height, key);
        AddInteriorKeyColorIslands(data, width, height, key, mask);
        return mask;
    }

    private static byte[] BuildConnectedBackgroundMask(byte[] data, int width, int height, Color key)
    {
        var pixelCount = width * height;
        var mask = new byte[pixelCount];
        var visited = new byte[pixelCount];
        var queue = new int[pixelCount];
        var queueStart = 0; var queueEnd = 0;

        void Enqueue(int index)
        {
            if (visited[index] != 0) return;
            visited[index] = 1;
            if (GetBackgroundConfidence(data, index, key) < 0.18) return;
            mask[index] = 1;
            queue[queueEnd++] = index;
        }

        for (var x = 0; x < width; x++) { Enqueue(x); Enqueue((height - 1) * width + x); }
        for (var y = 1; y < height - 1; y++) { Enqueue(y * width); Enqueue(y * width + width - 1); }

        while (queueStart < queueEnd)
        {
            var index = queue[queueStart++];
            var x = index % width;
            var y = index / width;
            if (x > 0) Enqueue(index - 1);
            if (x < width - 1) Enqueue(index + 1);
            if (y > 0) Enqueue(index - width);
            if (y < height - 1) Enqueue(index + width);
        }
        return mask;
    }

    private static void AddInteriorKeyColorIslands(byte[] data, int width, int height, Color key, byte[] mask)
    {
        var pixelCount = width * height;
        var visited = new byte[pixelCount];
        var queue = new int[pixelCount];
        var component = new int[pixelCount];

        for (var seed = 0; seed < pixelCount; seed++)
        {
            if (mask[seed] != 0 || visited[seed] != 0) continue;
            if (GetBackgroundConfidence(data, seed, key) < 0.68) continue;

            var queueStart = 0; var queueEnd = 0;
            var componentLength = 0;
            double confidenceSum = 0;
            var strictCount = 0; var strongCount = 0;

            visited[seed] = 1;
            queue[queueEnd++] = seed;

            void EnqueueNeighbor(int neighbor)
            {
                if (neighbor < 0 || mask[neighbor] != 0 || visited[neighbor] != 0) return;
                if (GetBackgroundConfidence(data, neighbor, key) < 0.24) return;
                visited[neighbor] = 1;
                queue[queueEnd++] = neighbor;
            }

            while (queueStart < queueEnd)
            {
                var index = queue[queueStart++];
                var confidence = GetBackgroundConfidence(data, index, key);
                component[componentLength++] = index;
                confidenceSum += confidence;
                if (confidence >= 0.68) strictCount++;
                if (confidence >= 0.86) strongCount++;

                var x = index % width;
                var y = index / width;
                if (x > 0) EnqueueNeighbor(index - 1);
                if (x < width - 1) EnqueueNeighbor(index + 1);
                if (y > 0) EnqueueNeighbor(index - width);
                if (y < height - 1) EnqueueNeighbor(index + width);
            }

            var averageConfidence = confidenceSum / componentLength;
            var strictRatio = strictCount / (double)componentLength;
            var strongRatio = strongCount / (double)componentLength;
            var shouldRemove = averageConfidence >= 0.42 || strictRatio >= 0.18 || strongRatio >= 0.05 ||
                (componentLength <= 3 && averageConfidence >= 0.34);

            if (shouldRemove)
            {
                for (var i = 0; i < componentLength; i++) mask[component[i]] = 1;
            }
        }
    }

    private static void WriteTransparentPixels(byte[] data, byte[] mask, int width, int height, Color key)
    {
        var distanceToBackground = ComputeDistanceToBackground(mask, width, height, 4);
        var pixelCount = width * height;

        for (var index = 0; index < pixelCount; index++)
        {
            var o = index * 4;
            var red = data[o + 2]; var green = data[o + 1]; var blue = data[o];
            var confidence = GetBackgroundConfidence(data, index, key);
            var alpha = 255;

            if (mask[index] != 0)
            {
                alpha = 0;
            }
            else
            {
                var distance = distanceToBackground[index];
                if (distance > 0)
                {
                    var transparency = GetEdgeTransparency(red, green, blue, confidence, distance, key);
                    if (transparency > 0) alpha = (int)Math.Round(255 * (1 - transparency));
                    alpha = Math.Max(alpha, distance == 1 ? 48 : distance == 2 ? 128 : 196);
                }
                else
                {
                    var isolatedSpill = GetKeyChannelMix(red, green, blue, key);
                    if (confidence >= 0.46 && isolatedSpill >= 0.45)
                    {
                        alpha = (int)Math.Round(255 * (1 - isolatedSpill * 0.75));
                        alpha = Math.Max(alpha, 96);
                    }
                }
            }

            var cleaned = RemoveColorSpill(red, green, blue, alpha, key, confidence, distanceToBackground[index]);
            data[o + 2] = ClampByte(cleaned.r);
            data[o + 1] = ClampByte(cleaned.g);
            data[o] = ClampByte(cleaned.b);
            data[o + 3] = (byte)alpha;
        }
    }

    private static byte[] ComputeDistanceToBackground(byte[] mask, int width, int height, int maxDistance)
    {
        var pixelCount = width * height;
        var distance = new byte[pixelCount];
        var frontier = new List<int>();

        for (var index = 0; index < pixelCount; index++)
        {
            if (mask[index] != 0) continue;
            var x = index % width;
            var y = index / width;
            var touches = (x > 0 && mask[index - 1] != 0) || (x < width - 1 && mask[index + 1] != 0) ||
                          (y > 0 && mask[index - width] != 0) || (y < height - 1 && mask[index + width] != 0);
            if (touches)
            {
                distance[index] = 1;
                frontier.Add(index);
            }
        }

        for (var currentDistance = 1; currentDistance < maxDistance; currentDistance++)
        {
            var next = new List<int>();
            foreach (var index in frontier)
            {
                var x = index % width;
                var y = index / width;
                if (x > 0) AddDistance(distance, mask, next, index - 1, currentDistance);
                if (x < width - 1) AddDistance(distance, mask, next, index + 1, currentDistance);
                if (y > 0) AddDistance(distance, mask, next, index - width, currentDistance);
                if (y < height - 1) AddDistance(distance, mask, next, index + width, currentDistance);
            }
            frontier = next;
            if (frontier.Count == 0) break;
        }
        return distance;
    }

    private static void AddDistance(byte[] distance, byte[] mask, List<int> next, int neighbor, int currentDistance)
    {
        if (neighbor < 0 || mask[neighbor] != 0 || distance[neighbor] != 0) return;
        distance[neighbor] = (byte)(currentDistance + 1);
        next.Add(neighbor);
    }

    private static double GetBackgroundConfidence(byte[] data, int index, Color key)
    {
        var o = index * 4;
        var d = Dist(data[o + 2], data[o + 1], data[o], key.R, key.G, key.B);
        return Clamp01((150 - d) / 150);
    }

    private static double GetEdgeTransparency(int r, int g, int b, double confidence, int distance, Color key)
    {
        var edgeStrength = distance <= 1 ? 1 : distance == 2 ? 0.75 : distance == 3 ? 0.45 : 0.25;
        var distanceEstimate = Clamp01(((confidence - 0.08) / 0.84) * edgeStrength);
        var channelEstimate = GetKeyChannelMix(r, g, b, key) * edgeStrength;
        return Clamp01(Math.Max(distanceEstimate, channelEstimate));
    }

    private static double GetKeyChannelMix(int r, int g, int b, Color key)
    {
        if (key.G == 255) return Clamp01((g - Math.Min(r, b)) / 255.0);
        return Clamp01((Math.Min(r, b) - g * 0.65) / 255.0);
    }

    private static (int r, int g, int b) RemoveColorSpill(int r, int g, int b, int alpha, Color key, double confidence, int distance)
    {
        if (alpha == 0) return (r, g, b);

        var edgeStrength = distance <= 0
            ? confidence >= 0.46 ? 0.35 : 0
            : distance == 1 ? 0.55 : distance == 2 ? 0.32 : 0.16;
        var spillMix = GetKeyChannelMix(r, g, b, key) * edgeStrength;
        var backgroundMix = Clamp01(Math.Max((255 - alpha) / 255.0, Math.Max(((confidence - 0.1) / 0.9) * edgeStrength, spillMix)));
        if (backgroundMix <= 0) return (r, g, b);

        var foregroundMix = Math.Max(0.08, 1 - backgroundMix);
        return (
            (int)((r - key.R * backgroundMix) / foregroundMix),
            (int)((g - key.G * backgroundMix) / foregroundMix),
            (int)((b - key.B * backgroundMix) / foregroundMix)
        );
    }

    private static double Clamp01(double v) => Math.Max(0, Math.Min(1, v));
    private static byte ClampByte(int v) => (byte)Math.Max(0, Math.Min(255, v));
}
