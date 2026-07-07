namespace VideoCortex.Features.Settings.Models;

/// <summary>
/// Bind target for the Settings page. Secrets are configured server-side (user-secrets in
/// development, an environment file in production — see deploy/README.md) and are never
/// entered or echoed here; the form only knows whether a secret <c>*IsSet</c>.
/// </summary>
public sealed class SettingsForm
{
    // LLM endpoint
    public string Model { get; set; } = "gpt-4o-mini";
    public string BaseUrl { get; set; } = string.Empty;
    public bool ApiKeyIsSet { get; set; }
    public int LlmRequestTimeoutSeconds { get; set; } = 600;

    // Apify
    public bool ApifyTokenIsSet { get; set; }
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
