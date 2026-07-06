namespace VideoCortex.Features.Settings.Models;

/// <summary>
/// Bind target for the Settings page. Secrets are never echoed back — the form only knows whether
/// a secret <c>*IsSet</c>, and persists it only when the user typed a new value (<c>*Dirty</c>).
/// </summary>
public sealed class SettingsForm
{
    // LLM endpoint
    public string Model { get; set; } = "gpt-4o-mini";
    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public bool ApiKeyIsSet { get; set; }
    public bool ApiKeyDirty { get; set; }
    public int LlmRequestTimeoutSeconds { get; set; } = 600;

    // Apify
    public string ApifyToken { get; set; } = string.Empty;
    public bool ApifyTokenIsSet { get; set; }
    public bool ApifyTokenDirty { get; set; }
    public int ApifyRunTimeoutSeconds { get; set; } = 300;

    // Library
    public string LibraryRootPath { get; set; } = string.Empty;

    // Worker knobs
    public int TranscriptIdlePollSeconds { get; set; } = 10;
    public int TranscriptMaxRetryAttempts { get; set; } = 3;
    public int SummaryIdlePollSeconds { get; set; } = 10;
    public int SummaryMaxRetryAttempts { get; set; } = 3;
    public int ReportIdlePollSeconds { get; set; } = 10;
    public int ReportCoalesceDebounceSeconds { get; set; } = 10;
    public int ReportMaxRetryAttempts { get; set; } = 3;
}
