using System.Net.Http.Headers;

namespace VideoCortex.Core.Services.Llm;

/// <summary>
/// Holds a single <see cref="HttpClient"/> for an OpenAI-compatible endpoint and rebuilds it
/// only when the resolved endpoint (<c>base URL | API key | timeout</c>) changes. Callers read
/// their <see cref="Microsoft.Extensions.Options.IOptionsMonitor{TOptions}"/> value per call and
/// pass it in, so a Settings edit (overlay reload) takes effect on the next request with no
/// process restart.
/// </summary>
/// <remarks>
/// On an endpoint change the previous client reference is dropped rather than disposed —
/// disposing could cancel an in-flight call, and rebuilds are rare. The GC reclaims the
/// abandoned handler.
/// </remarks>
public sealed class OpenAiClientCache : IDisposable
{
    public const string DefaultBaseUrl = "https://api.openai.com";

    private readonly object _lock = new();
    private readonly HttpMessageHandler? _testHandler; // test seam only
    private HttpClient? _client;
    private string? _key;

    public OpenAiClientCache() { }

    /// <summary>Test-only: routes every built client through <paramref name="testHandler"/>.</summary>
    internal OpenAiClientCache(HttpMessageHandler testHandler) => _testHandler = testHandler;

    /// <summary>
    /// Returns a client for the endpoint, rebuilding only when the parameters differ from the
    /// cached client. Blank <paramref name="baseUrl"/> → <see cref="DefaultBaseUrl"/>. Throws
    /// <see cref="InvalidOperationException"/> when the API key is blank.
    /// </summary>
    public HttpClient Get(string? baseUrl, string? apiKey, int timeoutSeconds)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException(
                "LLM API key is not configured. Set Llm:ApiKey via Settings, user-secrets, or environment variables.");

        var effectiveBase = string.IsNullOrWhiteSpace(baseUrl) ? DefaultBaseUrl : baseUrl!.Trim();
        var key = $"{effectiveBase}|{apiKey}|{timeoutSeconds}";

        lock (_lock)
        {
            if (_client is not null && _key == key)
                return _client;

            var client = _testHandler is null
                ? new HttpClient()
                : new HttpClient(_testHandler, disposeHandler: false);
            client.BaseAddress = new Uri(effectiveBase);
            client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            _client = client;
            _key = key;
            return client;
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _client?.Dispose();
            _client = null;
            _key = null;
        }
    }
}
