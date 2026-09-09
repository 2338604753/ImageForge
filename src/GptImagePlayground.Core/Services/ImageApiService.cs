using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using GptImagePlayground.Core.Models;

namespace GptImagePlayground.Core.Services;

/// <summary>图片生成 API 请求。</summary>
public class ImageApiRequest
{
    public AppSettings Settings { get; set; } = new();
    public string Prompt { get; set; } = "";
    public TaskParams Params { get; set; } = new();

    /// <summary>参考图（用于编辑）。</summary>
    public List<byte[]> ReferenceImages { get; set; } = new();
    public byte[]? Mask { get; set; }

    public bool NativeTransparent { get; set; }

    /// <summary>流式中间步骤图片回调（未启用流式时为 null）。</summary>
    public Action<byte[]>? OnPartialImage { get; set; }
}

/// <summary>图片生成 API 结果。</summary>
public class ImageApiResult
{
    public List<byte[]> Images { get; set; } = new();
    public List<string> RawImageUrls { get; set; } = new();
    public TaskParams? ActualParams { get; set; }
    public List<string> RevisedPrompts { get; set; } = new();
    public List<OutputError> FailedRequests { get; set; } = new();
}

/// <summary>
/// 图片生成 API 调度与各类供应商调用（OpenAI 兼容 Images / Responses、自定义、fal.ai）。
/// </summary>
public class ImageApiService
{
    private const string PROMPT_REWRITE_GUARD_PREFIX =
        "Treat everything after this line as one complete image-generation prompt, including the resolution instruction. Follow it exactly without rewriting or omitting anything:";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http;

    public ImageApiService(HttpClient? http = null)
    {
        _http = http ?? new HttpClient(new HttpClientHandler { AutomaticDecompression = System.Net.DecompressionMethods.All })
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
    }

    public async Task<ImageApiResult> GenerateAsync(ImageApiRequest req, CancellationToken ct)
    {
        var profile = req.Settings.ActiveProfile;
        if (profile == null) throw new InvalidOperationException("请在设置中添加 API 配置");

        return profile.ProviderKind switch
        {
            ApiProviderKind.Fal => await CallFalApi(req, profile, ct),
            _ => await CallOpenAiCompatible(req, profile, ct)
        };
    }

    private async Task<ImageApiResult> CallOpenAiCompatible(ImageApiRequest req, ApiProfile profile, CancellationToken ct)
    {
        if (profile.ProviderKind == ApiProviderKind.Custom)
        {
            var custom = req.Settings.CustomProviders.FirstOrDefault(c => c.Id == profile.Provider);
            if (custom != null) return await CallCustomApi(req, profile, custom, ct);
        }

        return profile.ApiMode == ApiMode.Responses
            ? await CallResponsesImageApi(req, profile, ct)
            : await CallImagesApi(req, profile, ct);
    }

    // ===== OpenAI Images API (images/generations, images/edits) =====

    private async Task<ImageApiResult> CallImagesApi(ImageApiRequest req, ApiProfile profile, CancellationToken ct)
    {
        var n = req.Params.N > 0 ? req.Params.N : 1;
        if ((profile.CodexCli || profile.StreamImages) && n > 1)
            return await CallImagesApiConcurrent(req, profile, n, ct);

        return await CallImagesApiSingle(req, profile, ct);
    }

    private async Task<ImageApiResult> CallImagesApiConcurrent(ImageApiRequest req, ApiProfile profile, int n, CancellationToken ct)
    {
        var tasks = new List<Task<ImageApiResult>>();
        for (var i = 0; i < n; i++)
        {
            var single = new ImageApiRequest
            {
                Settings = req.Settings,
                Prompt = req.Prompt,
                Params = CloneParams(req.Params, n: 1, quality: profile.CodexCli ? "auto" : req.Params.Quality),
                ReferenceImages = req.ReferenceImages,
                Mask = req.Mask,
                NativeTransparent = req.NativeTransparent,
                OnPartialImage = req.OnPartialImage
            };
            tasks.Add(CallImagesApiSingle(single, profile, ct, i));
        }

        var results = await Task.WhenAll(tasks);
        return MergeConcurrentResults(results);
    }

    private async Task<ImageApiResult> CallImagesApiSingle(ImageApiRequest req, ApiProfile profile, CancellationToken ct, int requestIndex = 0)
    {
        var prompt = BuildPrompt(req, profile);
        var isEdit = req.ReferenceImages.Count > 0;
        var path = isEdit ? "images/edits" : "images/generations";
        var url = BaseUrlHelper.BuildApiUrl(profile.BaseUrl, path);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(profile.Timeout));

        HttpResponseMessage response;
        if (isEdit)
        {
            response = await SendEditRequest(req, profile, url, timeoutCts.Token);
        }
        else
        {
            var body = BuildGenerateBody(req, profile);
            var json = JsonSerializer.Serialize(body, JsonOptions);
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            request.Headers.TryAddWithoutValidation("Authorization", BuildAuthHeader(profile));
            response = await _http.SendAsync(request, timeoutCts.Token);
        }

        if (!response.IsSuccessStatusCode)
        {
            var serverMsg = await GetApiErrorMessage(response);
            throw new ApiException(
                $"{serverMsg}\n\n调用地址：{url}\n使用的 Key：{KeyTail(profile)}",
                (int)response.StatusCode, req.Settings);
        }

        return profile.StreamImages
            ? await ParseImagesStream(response, req, profile, ct)
            : await ParseImagesResponse(response, req, profile, ct);
    }

    private async Task<HttpResponseMessage> SendEditRequest(ImageApiRequest req, ApiProfile profile, string url, CancellationToken ct)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(profile.Model), "model");
        form.Add(new StringContent(BuildPrompt(req, profile)), "prompt");
        if (!profile.CodexCli) form.Add(new StringContent(req.Params.Size), "size");
        else if (req.Params.Size != "auto") form.Add(new StringContent(req.Params.Size), "size");
        form.Add(new StringContent(req.Params.OutputFormat), "output_format");
        form.Add(new StringContent(req.Params.Moderation), "moderation");
        if (req.NativeTransparent) form.Add(new StringContent("transparent"), "background");
        if (!profile.CodexCli) form.Add(new StringContent(req.Params.Quality), "quality");
        if (req.Params.OutputFormat != "png" && req.Params.OutputCompression != null)
            form.Add(new StringContent(req.Params.OutputCompression.Value.ToString()), "output_compression");
        if (req.Params.N > 1) form.Add(new StringContent(req.Params.N.ToString()), "n");
        if (profile.ResponseFormatB64Json) form.Add(new StringContent("b64_json"), "response_format");

        var ext = GetExtension(req.Params.OutputFormat);
        for (var i = 0; i < req.ReferenceImages.Count; i++)
        {
            var blob = new ByteArrayContent(req.ReferenceImages[i]);
            blob.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
            form.Add(blob, "image[]", $"input-{i + 1}.{ext}");
        }
        if (req.Mask != null)
        {
            var mask = new ByteArrayContent(req.Mask);
            mask.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
            form.Add(mask, "mask", "mask.png");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = form };
        request.Headers.TryAddWithoutValidation("Authorization", BuildAuthHeader(profile));
        return await _http.SendAsync(request, ct);
    }

    private Dictionary<string, object?> BuildGenerateBody(ImageApiRequest req, ApiProfile profile)
    {
        var body = new Dictionary<string, object?>
        {
            ["model"] = profile.Model,
            ["prompt"] = BuildPrompt(req, profile),
            ["output_format"] = req.Params.OutputFormat,
            ["moderation"] = req.Params.Moderation
        };
        if (req.NativeTransparent) body["background"] = "transparent";
        if (!profile.CodexCli) body["size"] = req.Params.Size;
        if (!profile.CodexCli) body["quality"] = req.Params.Quality;
        if (req.Params.OutputFormat != "png" && req.Params.OutputCompression != null)
            body["output_compression"] = req.Params.OutputCompression;
        if (req.Params.N > 1) body["n"] = req.Params.N;
        if (profile.ResponseFormatB64Json) body["response_format"] = "b64_json";
        if (profile.StreamImages)
        {
            body["stream"] = true;
            body["partial_images"] = profile.StreamPartialImages;
        }
        return body;
    }

    private string BuildPrompt(ImageApiRequest req, ApiProfile profile)
    {
        var prompt = req.Prompt;
        if (profile.CodexCli && req.Params.Size != "auto")
            prompt = $"Generate at {req.Params.Size} resolution. {prompt}";
        if (profile.CodexCli && !req.Settings.AllowPromptRewrite)
            prompt = $"{PROMPT_REWRITE_GUARD_PREFIX}\n{prompt}";
        return prompt;
    }

    private async Task<ImageApiResult> ParseImagesResponse(HttpResponseMessage response, ImageApiRequest req, ApiProfile profile, CancellationToken ct)
    {
        var payload = JsonSerializer.Deserialize<ImageApiResponse>(await response.Content.ReadAsStringAsync(ct));
        if (payload?.Data == null || payload.Data.Count == 0)
            throw new ApiException("接口没有返回图片数据，请查看原始响应内容确认服务商实际返回的数据结构。如果使用的是中转或兼容接口，建议创建并使用「自定义服务商」配置。", (int)response.StatusCode, req.Settings);

        var result = new ImageApiResult();
        var mime = GetMime(req.Params.OutputFormat);
        foreach (var item in payload.Data)
        {
            if (!string.IsNullOrWhiteSpace(item.B64Json))
            {
                result.Images.Add(Convert.FromBase64String(item.B64Json!));
            }
            else if (IsHttpUrl(item.Url))
            {
                result.RawImageUrls.Add(item.Url!);
                result.Images.Add(await FetchAsBytes(item.Url!, mime, ct));
            }
            result.RevisedPrompts.Add(item.RevisedPrompt ?? "");
        }

        result.ActualParams = MergeActualParams(PickActualParams(payload));
        return result;
    }

    // ===== OpenAI Responses API =====

    private async Task<ImageApiResult> CallResponsesImageApi(ImageApiRequest req, ApiProfile profile, CancellationToken ct)
    {
        var n = req.Params.N > 0 ? req.Params.N : 1;
        if (n > 1)
        {
            var tasks = new List<Task<ImageApiResult>>();
            for (var i = 0; i < n; i++)
            {
                var single = new ImageApiRequest { Settings = req.Settings, Prompt = req.Prompt, Params = CloneParams(req.Params, n: 1), ReferenceImages = req.ReferenceImages, Mask = req.Mask, NativeTransparent = req.NativeTransparent, OnPartialImage = req.OnPartialImage };
                tasks.Add(CallResponsesImageApiSingle(single, profile, ct));
            }
            return MergeConcurrentResults(await Task.WhenAll(tasks));
        }
        return await CallResponsesImageApiSingle(req, profile, ct);
    }

    private async Task<ImageApiResult> CallResponsesImageApiSingle(ImageApiRequest req, ApiProfile profile, CancellationToken ct)
    {
        var url = BaseUrlHelper.BuildApiUrl(profile.BaseUrl, "responses");
        var isEdit = req.ReferenceImages.Count > 0;

        object input = BuildResponsesInput(req, profile);
        var tool = BuildResponsesImageTool(req, profile, isEdit);
        var body = new Dictionary<string, object?>
        {
            ["model"] = profile.Model,
            ["input"] = input,
            ["tools"] = new[] { tool },
            ["tool_choice"] = "required"
        };
        if (!string.IsNullOrEmpty(profile.ReasoningEffort)) body["reasoning"] = new Dictionary<string, object?> { ["effort"] = profile.ReasoningEffort };
        if (profile.StreamImages) body["stream"] = true;

        var json = JsonSerializer.Serialize(body, JsonOptions);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        content.Headers.TryAddWithoutValidation("Authorization", BuildAuthHeader(profile));

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(profile.Timeout));
        var response = await _http.PostAsync(url, content, timeoutCts.Token);

        if (!response.IsSuccessStatusCode)
            throw new ApiException(await GetApiErrorMessage(response), (int)response.StatusCode, req.Settings);

        var payload = JsonSerializer.Deserialize<ResponsesApiResponse>(await response.Content.ReadAsStringAsync(ct));
        return ParseResponsesImageResults(payload, req, profile);
    }

    private object BuildResponsesInput(ImageApiRequest req, ApiProfile profile)
    {
        var prompt = profile.ApiMode == ApiMode.Responses && profile.CodexCli && req.Params.Size != "auto"
            ? $"Generate at {req.Params.Size} resolution. {req.Prompt}"
            : req.Prompt;
        if (!req.Settings.AllowPromptRewrite) prompt = $"{PROMPT_REWRITE_GUARD_PREFIX}\n{prompt}";

        if (req.ReferenceImages.Count == 0) return prompt;

        var content = new List<object>
        {
            new Dictionary<string, object?> { ["type"] = "input_text", ["text"] = prompt }
        };
        foreach (var _ in req.ReferenceImages)
            content.Add(new Dictionary<string, object?> { ["type"] = "input_image", ["image_url"] = "data:image/png;base64,PLACEHOLDER" });

        return new List<object>
        {
            new Dictionary<string, object?> { ["role"] = "user", ["content"] = content }
        };
    }

    private Dictionary<string, object?> BuildResponsesImageTool(ImageApiRequest req, ApiProfile profile, bool isEdit)
    {
        var tool = new Dictionary<string, object?>
        {
            ["type"] = "image_generation",
            ["action"] = isEdit ? "edit" : "generate",
            ["output_format"] = req.Params.OutputFormat,
            ["moderation"] = req.Params.Moderation
        };
        var imageModel = profile.ImageGenerationModel;
        if (!string.IsNullOrWhiteSpace(imageModel)) tool["model"] = imageModel;
        if (!profile.CodexCli) tool["size"] = req.Params.Size;
        if (profile.StreamImages) tool["partial_images"] = profile.StreamPartialImages;
        if (!profile.CodexCli) tool["quality"] = req.Params.Quality;
        if (req.Params.OutputFormat != "png" && req.Params.OutputCompression != null)
            tool["output_compression"] = req.Params.OutputCompression;
        if (req.NativeTransparent) tool["background"] = "transparent";
        if (req.Mask != null)
            tool["input_image_mask"] = new Dictionary<string, object?> { ["image_url"] = "data:image/png;base64,PLACEHOLDER" };
        return tool;
    }

    private ImageApiResult ParseResponsesImageResults(ResponsesApiResponse? payload, ImageApiRequest req, ApiProfile profile)
    {
        if (payload?.Output == null || payload.Output.Count == 0)
            throw new ApiException("接口没有返回可识别的图片数据，请查看原始响应内容确认服务商实际返回的数据结构。如果使用的是中转或兼容接口，建议创建并使用「自定义服务商」配置。", 200, req.Settings);

        var result = new ImageApiResult();
        var mime = GetMime(req.Params.OutputFormat);
        foreach (var item in payload.Output)
        {
            if (item?.Type != "image_generation_call") continue;
            if (!string.IsNullOrWhiteSpace(item.Result))
            {
                result.Images.Add(Convert.FromBase64String(item.Result));
                result.RevisedPrompts.Add(item.RevisedPrompt ?? "");
            }
        }

        if (result.Images.Count == 0)
            throw new ApiException("接口没有返回可识别的图片数据，请查看原始响应内容确认服务商实际返回的数据结构。如果使用的是中转或兼容接口，建议创建并使用「自定义服务商」配置。", 200, req.Settings);

        var first = payload.Output.FirstOrDefault(x => x?.Type == "image_generation_call");
        result.ActualParams = MergeActualParams(PickActualParams(first ?? new ResponsesOutputItem()));
        return result;
    }

    // ===== 自定义供应商（提交/轮询） =====

    private async Task<ImageApiResult> CallCustomApi(ImageApiRequest req, ApiProfile profile, CustomProviderDefinition custom, CancellationToken ct)
    {
        var isEdit = req.ReferenceImages.Count > 0;
        var submit = (isEdit && custom.EditSubmit?.Path != null) ? custom.EditSubmit : custom.Submit;
        var context = BuildCustomContext(req, profile);

        // 构建请求体（模板变量替换）
        var resolved = ResolveTemplate(submit.Body, context);
        var payload = await SubmitCustom(submit, profile, resolved as object, req, ct);

        if (!string.IsNullOrEmpty(submit.TaskIdPath))
        {
            var taskId = GetByPath(payload, submit.TaskIdPath)?.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(taskId))
                throw new ApiException("无法从响应中提取异步任务 ID，请查看原始响应内容确认接口实际返回的数据结构。", 200, req.Settings);
            var poll = custom.Poll;
            if (poll == null || string.IsNullOrEmpty(poll.Path))
                throw new ApiException("异步接口返回了 task_id，但服务商配置缺少 poll。", 200, req.Settings);
            return await PollCustom(poll, profile, taskId, req, ct);
        }

        return await ExtractCustomImages(payload, submit.Result, req, ct);
    }

    private static Dictionary<string, object?> BuildCustomContext(ImageApiRequest req, ApiProfile profile)
    {
        var prompt = req.Prompt;
        if (profile.CodexCli && req.Params.Size != "auto") prompt = $"Generate at {req.Params.Size} resolution. {prompt}";
        var paramsDict = new Dictionary<string, object?>
        {
            ["size"] = req.Params.Size,
            ["quality"] = req.Params.Quality,
            ["output_format"] = req.Params.OutputFormat,
            ["output_compression"] = req.Params.OutputCompression,
            ["moderation"] = req.Params.Moderation,
            ["n"] = req.Params.N
        };
        if (req.NativeTransparent) paramsDict["background"] = "transparent";

        return new Dictionary<string, object?>
        {
            ["profile"] = profile,
            ["prompt"] = prompt,
            ["params"] = paramsDict,
            ["inputImages"] = new Dictionary<string, object?> { ["dataUrls"] = req.ReferenceImages.Count > 0 ? req.ReferenceImages.Count : null, ["count"] = req.ReferenceImages.Count },
            ["mask"] = new Dictionary<string, object?> { ["dataUrl"] = req.Mask != null ? "PLACEHOLDER" : null }
        };
    }

    private async Task<Dictionary<string, object?>> SubmitCustom(CustomProviderSubmitMapping submit, ApiProfile profile, object? resolved, ImageApiRequest req, CancellationToken ct)
    {
        var url = BaseUrlHelper.BuildApiUrl(profile.BaseUrl, submit.Path);
        var method = submit.Method == CustomProviderRequestMethod.Get ? HttpMethod.Get : HttpMethod.Post;
        using var request = new HttpRequestMessage(method, url);
        request.Headers.TryAddWithoutValidation("Authorization", BuildAuthHeader(profile));
        if (method != HttpMethod.Get)
        {
            var json = JsonSerializer.Serialize(resolved, JsonOptions);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(profile.Timeout));
        var response = await _http.SendAsync(request, timeoutCts.Token);
        if (!response.IsSuccessStatusCode)
            throw new ApiException(await GetApiErrorMessage(response), (int)response.StatusCode, req.Settings);
        return JsonSerializer.Deserialize<Dictionary<string, object?>>(await response.Content.ReadAsStringAsync(ct)) ?? new();
    }

    private async Task<ImageApiResult> PollCustom(CustomProviderPollMapping poll, ApiProfile profile, string taskId, ImageApiRequest req, CancellationToken ct)
    {
        var path = poll.Path.Replace("{task_id}", Uri.EscapeDataString(taskId)).Replace("{taskId}", Uri.EscapeDataString(taskId));
        var url = BaseUrlHelper.BuildApiUrl(profile.BaseUrl, path);
        var mime = GetMime(req.Params.OutputFormat);

        while (true)
        {
            ct.ThrowIfCancellationRequested();
            using var request = new HttpRequestMessage(poll.Method == CustomProviderRequestMethod.Get ? HttpMethod.Get : HttpMethod.Post, url);
            request.Headers.TryAddWithoutValidation("Authorization", BuildAuthHeader(profile));
            using var response = await _http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) throw new ApiException(await GetApiErrorMessage(response), (int)response.StatusCode, req.Settings);

            var payload = JsonSerializer.Deserialize<Dictionary<string, object?>>(await response.Content.ReadAsStringAsync(ct)) ?? new();
            var state = GetTaskState(payload, poll);
            if (state == "failure")
            {
                var msg = GetByPath(payload, poll.ErrorPath)?.ToString() ?? "异步任务失败";
                throw new ApiException(msg, (int)response.StatusCode, req.Settings);
            }
            if (state == "success")
                return await ExtractCustomImages(payload, poll.Result, req, ct);

            await Task.Delay(TimeSpan.FromSeconds(poll.IntervalSeconds > 0 ? poll.IntervalSeconds : 5), ct);
        }
    }

    private async Task<ImageApiResult> ExtractCustomImages(Dictionary<string, object?> payload, CustomProviderResultMapping? result, ImageApiRequest req, CancellationToken ct)
    {
        if (result == null) throw new ApiException("自定义服务商缺少结果提取配置。", 200, req.Settings);
        var outResult = new ImageApiResult();
        var mime = GetMime(req.Params.OutputFormat);

        foreach (var path in result.B64JsonPaths)
        {
            foreach (var value in GetAllByPath(payload, path))
                if (value is string s && !string.IsNullOrWhiteSpace(s)) outResult.Images.Add(Convert.FromBase64String(s));
        }
        foreach (var path in result.ImageUrlPaths)
        {
            foreach (var value in GetAllByPath(payload, path))
                if (value is string u && IsHttpUrl(u)) { outResult.RawImageUrls.Add(u); outResult.Images.Add(await FetchAsBytes(u, mime, ct)); }
        }

        if (outResult.Images.Count == 0)
            throw new ApiException("接口没有返回可识别的图片数据，请根据 API 文档调整「自定义服务商」配置中的结果提取路径。", 200, req.Settings);
        return outResult;
    }

    // ===== fal.ai（预留，简单封装） =====

    private async Task<ImageApiResult> CallFalApi(ImageApiRequest req, ApiProfile profile, CancellationToken ct)
    {
        throw new ApiException("fal.ai 供应商暂未实现，请先使用 OpenAI 兼容接口或自定义供应商。", 501, req.Settings);
    }

    // ===== 通用辅助 =====

    private async Task<byte[]> FetchAsBytes(string url, string mime, CancellationToken ct)
    {
        var resp = await _http.GetAsync(url, ct);
        if (!resp.IsSuccessStatusCode) throw new ApiException($"图片 URL 下载失败：HTTP {(int)resp.StatusCode}", (int)resp.StatusCode, null);
        return await resp.Content.ReadAsByteArrayAsync(ct);
    }

    private async Task<ImageApiResult> ParseImagesStream(HttpResponseMessage response, ImageApiRequest req, ApiProfile profile, CancellationToken ct)
    {
        // 流式：读取 SSE 行，收集 partial + 最终结果
        var finalPayload = new ImageApiResponse();
        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);
        var collected = new List<ResponsesOutputItem>();
        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync();
            if (line == null || !line.StartsWith("data:")) continue;
            var data = line.Substring(5).Trim();
            if (data == "[DONE]" || data.Length == 0) continue;
            var evt = JsonSerializer.Deserialize<JsonElement>(data);
            var type = evt.TryGetProperty("type", out var t) ? t.GetString() : null;
            var obj = evt.TryGetProperty("object", out var o) ? o.GetString() : null;
            if (type == "image_generation.partial_image" || type == "image_edit.partial_image")
            {
                if (evt.TryGetProperty("b64_json", out var b64) && b64.ValueKind == JsonValueKind.String)
                    req.OnPartialImage?.Invoke(Convert.FromBase64String(b64.GetString()!));
            }
            else if (obj == "image.generation.result" || obj == "image.edit.result")
            {
                finalPayload = JsonSerializer.Deserialize<ImageApiResponse>(data) ?? finalPayload;
            }
            else if (type == "image_generation.completed" || type == "image_edit.completed")
            {
                finalPayload.Data ??= new();
                if (evt.TryGetProperty("b64_json", out var b64) && b64.ValueKind == JsonValueKind.String)
                    finalPayload.Data.Add(new ImageResponseItem { B64Json = b64.GetString()! });
                if (evt.TryGetProperty("url", out var u) && u.ValueKind == JsonValueKind.String)
                    finalPayload.Data.Add(new ImageResponseItem { Url = u.GetString() });
            }
        }

        if (finalPayload.Data?.Count > 0) return await ParseImagesResponseFromPayload(finalPayload, req, profile);
        if (collected.Count > 0) return ParseResponsesImageResults(new ResponsesApiResponse { Output = collected }, req, profile);
        throw new ApiException("流式接口未返回最终图片数据", (int)response.StatusCode, req.Settings);
    }

    private Task<ImageApiResult> ParseImagesResponseFromPayload(ImageApiResponse payload, ImageApiRequest req, ApiProfile profile)
    {
        var result = new ImageApiResult();
        var mime = GetMime(req.Params.OutputFormat);
        foreach (var item in payload.Data ?? new())
        {
            if (!string.IsNullOrWhiteSpace(item.B64Json)) result.Images.Add(Convert.FromBase64String(item.B64Json!));
            else if (IsHttpUrl(item.Url)) result.RawImageUrls.Add(item.Url!);
            result.RevisedPrompts.Add(item.RevisedPrompt ?? "");
        }
        if (result.Images.Count == 0 && result.RawImageUrls.Count == 0)
            throw new ApiException("流式接口未返回图片数据", 200, req.Settings);
        return Task.FromResult(result);
    }

    private async Task<string> GetApiErrorMessage(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        try
        {
            var doc = JsonDocument.Parse(text);
            if (doc.RootElement.TryGetProperty("error", out var err))
            {
                if (err.ValueKind == JsonValueKind.Object && err.TryGetProperty("message", out var m)) return m.GetString() ?? $"HTTP {(int)response.StatusCode}";
                if (err.ValueKind == JsonValueKind.String) return err.GetString() ?? $"HTTP {(int)response.StatusCode}";
            }
            if (doc.RootElement.TryGetProperty("message", out var msg)) return msg.GetString() ?? $"HTTP {(int)response.StatusCode}";
        }
        catch { }
        return string.IsNullOrWhiteSpace(text) ? $"HTTP {(int)response.StatusCode}" : text;
    }

    private static string BuildAuthHeader(ApiProfile profile)
    {
        var key = (profile.ApiKey ?? "").Trim();
        return "Bearer " + key;
    }

    private static string KeyTail(ApiProfile profile, int tail = 4)
    {
        var key = (profile.ApiKey ?? "").Trim();
        if (key.Length == 0) return "(空)";
        var shown = key.Length <= tail ? key : key.Substring(key.Length - tail);
        return new string('*', Math.Max(1, key.Length - tail)) + shown + $"（长度 {key.Length}）";
    }

    private static bool IsHttpUrl(string? s) => !string.IsNullOrEmpty(s) && (s.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || s.StartsWith("https://", StringComparison.OrdinalIgnoreCase));

    private static TaskParams? PickActualParams(object source)
    {
        var json = JsonSerializer.Serialize(source);
        var d = JsonSerializer.Deserialize<Dictionary<string, object?>>(json);
        if (d == null) return null;
        var p = new TaskParams();
        bool any = false;
        if (d.TryGetValue("size", out var sz) && sz is string s1) { p.Size = s1; any = true; }
        if (d.TryGetValue("quality", out var q) && q is string q1 && new[] { "auto", "low", "medium", "high", "xhigh", "max" }.Contains(q1)) { p.Quality = q1; any = true; }
        if (d.TryGetValue("output_format", out var of) && of is string of1) { p.OutputFormat = of1; any = true; }
        if (d.TryGetValue("output_compression", out var oc) && oc is JsonElement oce && oce.ValueKind == JsonValueKind.Number) { p.OutputCompression = oce.GetInt32(); any = true; }
        if (d.TryGetValue("moderation", out var mod) && mod is string mod1) { p.Moderation = mod1; any = true; }
        if (d.TryGetValue("n", out var nn) && nn is JsonElement nne) { if (nne.ValueKind == JsonValueKind.Number) { p.N = nne.GetInt32(); any = true; } }
        return any ? p : null;
    }

    private static TaskParams? MergeActualParams(params TaskParams?[] sources)
    {
        var merged = new TaskParams();
        bool any = false;
        foreach (var s in sources)
        {
            if (s == null) continue;
            if (!string.IsNullOrEmpty(s.Size) && s.Size != "auto") { merged.Size = s.Size; any = true; }
            if (s.Quality != "auto") { merged.Quality = s.Quality; any = true; }
            if (!string.IsNullOrEmpty(s.OutputFormat) && s.OutputFormat != "png") { merged.OutputFormat = s.OutputFormat; any = true; }
            if (s.OutputCompression != null) { merged.OutputCompression = s.OutputCompression; any = true; }
            if (s.Moderation != "auto") { merged.Moderation = s.Moderation; any = true; }
            if (s.N != 1) { merged.N = s.N; any = true; }
        }
        return any ? merged : null;
    }

    private static ImageApiResult MergeConcurrentResults(ImageApiResult[] results)
    {
        var ok = results.Where(r => r.Images.Count > 0).ToList();
        if (ok.Count == 0)
        {
            var err = results.FirstOrDefault(r => r.FailedRequests.Count > 0)?.FailedRequests.FirstOrDefault();
            throw new ApiException(err?.Error ?? "并发请求全部失败", 200, null);
        }
        var merged = new ImageApiResult();
        int idx = 0;
        foreach (var r in results)
        {
            if (r.Images.Count > 0)
            {
                merged.Images.AddRange(r.Images);
                merged.RawImageUrls.AddRange(r.RawImageUrls);
                merged.RevisedPrompts.AddRange(r.RevisedPrompts);
            }
            else
            {
                merged.FailedRequests.Add(new OutputError { RequestIndex = idx, Error = r.FailedRequests.FirstOrDefault()?.Error ?? "请求失败" });
            }
            idx++;
        }
        return merged;
    }

    private static string GetMime(string format) => format switch { "jpeg" => "image/jpeg", "webp" => "image/webp", _ => "image/png" };
    private static string GetExtension(string format) => format switch { "jpeg" => "jpg", "webp" => "webp", _ => "png" };

    private static object? GetByPath(object? source, string path)
    {
        if (string.IsNullOrEmpty(path)) return source;
        foreach (var key in path.Split('.').Where(k => k.Length > 0))
        {
            if (source is Dictionary<string, object?> dict && dict.TryGetValue(key, out var v)) { source = v; continue; }
            if (source is JsonElement je && je.ValueKind == JsonValueKind.Object && je.TryGetProperty(key, out var jv)) { source = jv; continue; }
            if (source is JsonElement arr && arr.ValueKind == JsonValueKind.Array && int.TryParse(key, out var idx) && idx >= 0 && idx < arr.GetArrayLength()) { source = arr[idx]; continue; }
            return null;
        }
        return source;
    }

    private static IEnumerable<object?> GetAllByPath(object source, string path)
    {
        var current = new List<object?> { source };
        foreach (var key in path.Split('.').Where(k => k.Length > 0))
        {
            var next = new List<object?>();
            foreach (var item in current)
            {
                if (item == null) continue;
                if (key == "*")
                {
                    if (item is IEnumerable<object> arr) next.AddRange(arr.Cast<object>());
                    continue;
                }
                if (item is Dictionary<string, object?> dict && dict.TryGetValue(key, out var v)) { next.Add(v); continue; }
                if (item is JsonElement je && je.ValueKind == JsonValueKind.Object && je.TryGetProperty(key, out var jv)) { next.Add(jv); continue; }
                if (item is JsonElement jar2 && jar2.ValueKind == JsonValueKind.Array && int.TryParse(key, out var idx) && idx >= 0 && idx < jar2.GetArrayLength()) { next.Add(jar2[idx]); continue; }
            }
            current = next;
        }
        return current.Where(x => x != null).ToList();
    }

    private static string GetTaskState(Dictionary<string, object?> payload, CustomProviderPollMapping poll)
    {
        var status = GetByPath(payload, poll.StatusPath)?.ToString() ?? "";
        if (poll.SuccessValues.Contains(status)) return "success";
        if (poll.FailureValues.Contains(status)) return "failure";
        return "pending";
    }

    private static object? ResolveTemplate(object? value, Dictionary<string, object?> context)
    {
        if (value is string s && s.StartsWith("$")) return GetByPath(context, s.Substring(1));
        if (value is Dictionary<string, object?> dict)
        {
            var outDict = new Dictionary<string, object?>();
            foreach (var kv in dict)
            {
                var resolved = ResolveTemplate(kv.Value, context);
                if (resolved == null || (resolved is Dictionary<string, object?> r && r.Count == 0)) continue;
                outDict[kv.Key] = resolved;
            }
            return outDict;
        }
        if (value is System.Collections.IEnumerable en && value is not string)
        {
            var list = new List<object?>();
            foreach (var item in en)
            {
                var resolved = ResolveTemplate(item, context);
                if (resolved != null) list.Add(resolved);
            }
            return list;
        }
        return value;
    }

    private static TaskParams CloneParams(TaskParams p, int? n = null, string? quality = null)
    {
        return new TaskParams
        {
            Size = p.Size,
            Quality = quality ?? p.Quality,
            OutputFormat = p.OutputFormat,
            OutputCompression = p.OutputCompression,
            Moderation = p.Moderation,
            N = n ?? p.N,
            TransparentOutput = p.TransparentOutput
        };
    }
}

/// <summary>API 调用异常（含状态码与设置上下文）。</summary>
public class ApiException : Exception
{
    public int StatusCode { get; }
    public AppSettings? Settings { get; }
    public ApiException(string message, int statusCode, AppSettings? settings) : base(message)
    {
        StatusCode = statusCode;
        Settings = settings;
    }
}


