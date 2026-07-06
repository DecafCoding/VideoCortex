using VideoCortex.Features.Settings.Models;

namespace VideoCortex.Features.Settings.Services;

/// <summary>Outcome of a settings save.</summary>
public sealed record SettingsResult(bool Ok, string? Error)
{
    public static SettingsResult Success() => new(true, null);
    public static SettingsResult Failure(string error) => new(false, error);
}

/// <summary>Loads current effective settings and persists edits to the writable overlay.</summary>
public interface ISettingsService
{
    /// <summary>Reads current effective values from configuration (secrets as set/not-set only).</summary>
    Task<SettingsForm> LoadAsync(CancellationToken ct = default);

    /// <summary>Validates then persists changed keys to the overlay. Secrets only when dirty.</summary>
    Task<SettingsResult> SaveAsync(SettingsForm form, CancellationToken ct = default);
}
