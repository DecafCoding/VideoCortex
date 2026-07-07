using Microsoft.Extensions.Configuration;
using VideoCortex.Core.Services.Config;
using VideoCortex.Features.Settings.Models;

namespace VideoCortex.Features.Settings.Services;

/// <summary>
/// Loads current effective settings from <see cref="IConfiguration"/> (appsettings + overlay) and
/// persists edits to the writable overlay via <see cref="IOverlayWriter"/>. Persisted keys use the
/// same colon paths the Phase-1 config records bind, so <c>IOptionsMonitor</c> picks changes up on
/// the next reload — no restart. Secrets are read-only here (surfaced as <c>*IsSet</c> flags) and
/// are never written to the overlay — they come from user-secrets (dev) or the environment (prod).
/// </summary>
public sealed class SettingsService(IConfiguration config, IOverlayWriter overlay) : ISettingsService
{
    public Task<SettingsForm> LoadAsync(CancellationToken ct = default)
    {
        var form = new SettingsForm
        {
            Model = config[$"{LlmSettings.Section}:Model"] ?? "gpt-4o-mini",
            BaseUrl = config[$"{LlmSettings.Section}:BaseUrl"] ?? string.Empty,
            ApiKeyIsSet = !string.IsNullOrEmpty(config[$"{LlmSettings.Section}:ApiKey"]),
            LlmRequestTimeoutSeconds = ReadInt($"{LlmSettings.Section}:RequestTimeoutSeconds", 600),

            ApifyTokenIsSet = !string.IsNullOrEmpty(config[$"{ApifySettings.Section}:Token"]),
            ApifyRunTimeoutSeconds = ReadInt($"{ApifySettings.Section}:RunTimeoutSeconds", 300),

            LibraryRootPath = config[$"{LibrarySettings.Section}:RootPath"] ?? VideoCortexPaths.DefaultLibraryRoot,

            TranscriptIdlePollSeconds = ReadInt($"{TranscriptWorkerSettings.Section}:IdlePollSeconds", 10),
            TranscriptMaxRetryAttempts = ReadInt($"{TranscriptWorkerSettings.Section}:MaxRetryAttempts", 3),
            SummaryIdlePollSeconds = ReadInt($"{SummarySettings.Section}:IdlePollSeconds", 10),
            SummaryMaxRetryAttempts = ReadInt($"{SummarySettings.Section}:MaxRetryAttempts", 3),
            ReportIdlePollSeconds = ReadInt($"{ReportSettings.Section}:IdlePollSeconds", 10),
            ReportCoalesceDebounceSeconds = ReadInt($"{ReportSettings.Section}:CoalesceDebounceSeconds", 10),
            ReportMaxRetryAttempts = ReadInt($"{ReportSettings.Section}:MaxRetryAttempts", 3),
        };
        return Task.FromResult(form);
    }

    public async Task<SettingsResult> SaveAsync(SettingsForm form, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(form);

        // -- Validation --
        if (string.IsNullOrWhiteSpace(form.Model))
            return SettingsResult.Failure("Model is required.");
        if (!string.IsNullOrWhiteSpace(form.BaseUrl) &&
            !Uri.TryCreate(form.BaseUrl, UriKind.Absolute, out _))
            return SettingsResult.Failure("Base URL must be a valid absolute URL (or blank for hosted OpenAI).");
        if (string.IsNullOrWhiteSpace(form.LibraryRootPath) || !Path.IsPathFullyQualified(form.LibraryRootPath))
            return SettingsResult.Failure("Library root must be an absolute (rooted) path.");

        foreach (var (name, poll) in new[]
                 {
                     ("Transcript poll", form.TranscriptIdlePollSeconds),
                     ("Summary poll", form.SummaryIdlePollSeconds),
                     ("Report poll", form.ReportIdlePollSeconds),
                 })
            if (poll < 1) return SettingsResult.Failure($"{name} interval must be ≥ 1 second.");

        foreach (var (name, cap) in new[]
                 {
                     ("Transcript retry cap", form.TranscriptMaxRetryAttempts),
                     ("Summary retry cap", form.SummaryMaxRetryAttempts),
                     ("Report retry cap", form.ReportMaxRetryAttempts),
                 })
            if (cap < 0) return SettingsResult.Failure($"{name} must be ≥ 0.");

        if (form.ReportCoalesceDebounceSeconds < 0)
            return SettingsResult.Failure("Report debounce must be ≥ 0 seconds.");
        if (form.LlmRequestTimeoutSeconds < 1 || form.ApifyRunTimeoutSeconds < 1)
            return SettingsResult.Failure("Timeouts must be ≥ 1 second.");

        // -- Persist (colon keys match the config record Sections) --
        await overlay.SetAsync($"{LlmSettings.Section}:Model", form.Model.Trim(), ct);
        await overlay.SetAsync($"{LlmSettings.Section}:BaseUrl", form.BaseUrl.Trim(), ct); // empty removes → hosted
        await overlay.SetAsync($"{LlmSettings.Section}:RequestTimeoutSeconds", form.LlmRequestTimeoutSeconds.ToString(), ct);
        await overlay.SetAsync($"{ApifySettings.Section}:RunTimeoutSeconds", form.ApifyRunTimeoutSeconds.ToString(), ct);
        await overlay.SetAsync($"{LibrarySettings.Section}:RootPath", form.LibraryRootPath.Trim(), ct);

        await overlay.SetAsync($"{TranscriptWorkerSettings.Section}:IdlePollSeconds", form.TranscriptIdlePollSeconds.ToString(), ct);
        await overlay.SetAsync($"{TranscriptWorkerSettings.Section}:MaxRetryAttempts", form.TranscriptMaxRetryAttempts.ToString(), ct);
        await overlay.SetAsync($"{SummarySettings.Section}:IdlePollSeconds", form.SummaryIdlePollSeconds.ToString(), ct);
        await overlay.SetAsync($"{SummarySettings.Section}:MaxRetryAttempts", form.SummaryMaxRetryAttempts.ToString(), ct);
        await overlay.SetAsync($"{ReportSettings.Section}:IdlePollSeconds", form.ReportIdlePollSeconds.ToString(), ct);
        await overlay.SetAsync($"{ReportSettings.Section}:CoalesceDebounceSeconds", form.ReportCoalesceDebounceSeconds.ToString(), ct);
        await overlay.SetAsync($"{ReportSettings.Section}:MaxRetryAttempts", form.ReportMaxRetryAttempts.ToString(), ct);

        return SettingsResult.Success();
    }

    private int ReadInt(string key, int fallback) =>
        int.TryParse(config[key], out var v) ? v : fallback;
}
