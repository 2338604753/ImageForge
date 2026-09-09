namespace GptImagePlayground.Core.Models;

public enum CustomProviderRequestMethod
{
    Get,
    Post
}

public enum CustomProviderContentType
{
    Json,
    Multipart
}

public enum CustomProviderFileSource
{
    InputImages,
    Mask
}

public class CustomProviderFileMapping
{
    public string Field { get; set; } = "";
    public CustomProviderFileSource Source { get; set; }
    public bool Array { get; set; }
}

public class CustomProviderResultMapping
{
    public List<string> ImageUrlPaths { get; set; } = new();
    public List<string> B64JsonPaths { get; set; } = new();
}

public class CustomProviderSubmitMapping
{
    public string Path { get; set; } = "";
    public CustomProviderRequestMethod Method { get; set; } = CustomProviderRequestMethod.Post;
    public CustomProviderContentType ContentType { get; set; } = CustomProviderContentType.Json;
    public Dictionary<string, string> Query { get; set; } = new();
    public Dictionary<string, object> Body { get; set; } = new();
    public List<CustomProviderFileMapping> Files { get; set; } = new();
    public string TaskIdPath { get; set; } = "";
    public CustomProviderResultMapping Result { get; set; } = new();
}

public class CustomProviderPollMapping
{
    public string Path { get; set; } = "";
    public CustomProviderRequestMethod Method { get; set; } = CustomProviderRequestMethod.Get;
    public Dictionary<string, string> Query { get; set; } = new();
    public int IntervalSeconds { get; set; } = 5;
    public string StatusPath { get; set; } = "";
    public List<string> SuccessValues { get; set; } = new();
    public List<string> FailureValues { get; set; } = new();
    public string ErrorPath { get; set; } = "";
    public CustomProviderResultMapping Result { get; set; } = new();
}

public class CustomProviderDefinition
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Template { get; set; } = "";
    public CustomProviderSubmitMapping Submit { get; set; } = new();
    public CustomProviderSubmitMapping EditSubmit { get; set; } = new();
    public CustomProviderPollMapping Poll { get; set; } = new();
}
