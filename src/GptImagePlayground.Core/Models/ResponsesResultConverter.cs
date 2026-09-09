using System.Text.Json;
using System.Text.Json.Serialization;

namespace GptImagePlayground.Core.Models;

/// <summary>
/// 解析 Responses API 的 result 字段：可能是纯 base64 字符串，也可能是
/// { b64_json | base64 | image | data } 对象。提取其中的 base64 值。
/// </summary>
public class ResponsesResultConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return reader.GetString();
        }

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            var root = doc.RootElement;
            foreach (var key in new[] { "b64_json", "base64", "image", "data" })
            {
                if (root.TryGetProperty(key, out var prop) && prop.ValueKind == JsonValueKind.String)
                {
                    var val = prop.GetString();
                    if (!string.IsNullOrWhiteSpace(val)) return val;
                }
            }
        }

        return null;
    }

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value);
    }
}
