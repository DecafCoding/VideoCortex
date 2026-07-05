using System.Net.Http.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using VideoCortex.Core.Services.Config;

namespace VideoCortex.Core.Services.Llm;

/// <summary>
/// Thin wrapper over any OpenAI-compatible <c>/v1/chat/completions</c> endpoint. Owns one
/// <see cref="OpenAiClientCache"/> so the underlying <see cref="HttpClient"/> is rebuilt only
/// when the endpoint changes. Shared by the summarizer (Phase 5) and the report synthesizer
/// (Phase 6).
/// </summary>
public sealed class OpenAiClient : IDisposable
{
    private readonly OpenAiClientCache _clients;

    public OpenAiClient() => _clients = new OpenAiClientCache();

    /// <summary>Test-only: routes requests through the supplied handler.</summary>
    internal OpenAiClient(HttpMessageHandler testHandler) => _clients = new OpenAiClientCache(testHandler);

    public async Task<ChatCompletionResponse> PostChatAsync(
        LlmSettings settings, ChatCompletionRequest request, CancellationToken ct = default)
    {
        var http = _clients.Get(settings.BaseUrl, settings.ApiKey, settings.RequestTimeoutSeconds);
        using var resp = await http.PostAsJsonAsync("/v1/chat/completions", request, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var errBody = await resp.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"OpenAI {(int)resp.StatusCode}: {errBody}");
        }

        var parsed = await resp.Content.ReadFromJsonAsync<ChatCompletionResponse>(cancellationToken: ct);
        return parsed ?? throw new HttpRequestException("OpenAI returned an empty response body.");
    }

    public void Dispose() => _clients.Dispose();
}

// -- OpenAI-compatible chat DTOs (shared across LLM stages) -----------------------------------

public sealed class ChatCompletionRequest
{
    [JsonPropertyName("model")] public string Model { get; set; } = "";
    [JsonPropertyName("messages")] public ChatMessage[] Messages { get; set; } = Array.Empty<ChatMessage>();

    [JsonPropertyName("response_format")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ResponseFormat? ResponseFormat { get; set; }
}

public sealed class ChatMessage
{
    [JsonPropertyName("role")] public string Role { get; set; } = "";
    [JsonPropertyName("content")] public string Content { get; set; } = "";
}

public sealed class ResponseFormat
{
    [JsonPropertyName("type")] public string Type { get; set; } = "";
    [JsonPropertyName("json_schema")] public JsonSchemaSpec? JsonSchema { get; set; }
}

public sealed class JsonSchemaSpec
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("strict")] public bool Strict { get; set; }
    [JsonPropertyName("schema")] public JsonNode? Schema { get; set; }
}

public sealed class ChatCompletionResponse
{
    [JsonPropertyName("choices")] public List<ChatChoice> Choices { get; set; } = new();
}

public sealed class ChatChoice
{
    [JsonPropertyName("message")] public ChatMessage? Message { get; set; }
}
