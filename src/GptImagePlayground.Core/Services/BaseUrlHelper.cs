namespace GptImagePlayground.Core.Services;

/// <summary>OpenAI 兼容的 Base URL 规格化与接口地址拼接。</summary>
public static class BaseUrlHelper
{
    public static string NormalizeBaseUrl(string baseUrl)
    {
        var trimmed = (baseUrl ?? "").Trim();
        if (trimmed.Length == 0) return "";

        var input = System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^[a-zA-Z][a-zA-Z\d+.-]*://")
            ? trimmed
            : $"https://{trimmed}";

        try
        {
            var url = new Uri(input);
            if (trimmed.EndsWith("/"))
            {
                var path = url.AbsolutePath.TrimEnd('/');
                return $"{url.Scheme}://{url.Authority}{path}/";
            }

            var segments = url.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries).ToList();
            var v1Index = segments.IndexOf("v1");
            var normalizedSegments = v1Index >= 0
                ? segments.Take(v1Index + 1).ToList()
                : segments.Count > 0
                    ? segments.Append("v1").ToList()
                    : new List<string>();
            var pathname = normalizedSegments.Count > 0 ? "/" + string.Join("/", normalizedSegments) : "";
            return $"{url.Scheme}://{url.Authority}{pathname}";
        }
        catch
        {
            return trimmed.TrimEnd('/');
        }
    }

    public static string BuildApiUrl(string baseUrl, string path)
    {
        var trimmedBase = (baseUrl ?? "").Trim();
        var endpointPath = path.TrimStart('/');

        if (trimmedBase.Length == 0) return "/" + endpointPath;

        var normalized = NormalizeBaseUrl(trimmedBase);
        if (trimmedBase.EndsWith("/"))
        {
            return $"{normalized.TrimEnd('/')}/{endpointPath}";
        }

        var apiPath = normalized.EndsWith("/v1")
            ? endpointPath
            : $"v1/{endpointPath}";

        return $"{normalized.TrimEnd('/')}/{apiPath}";
    }
}
