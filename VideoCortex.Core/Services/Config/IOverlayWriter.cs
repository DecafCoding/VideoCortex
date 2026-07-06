using System.Text.Json.Nodes;

namespace VideoCortex.Core.Services.Config;

/// <summary>
/// Reads and writes the highest-priority writable config overlay
/// (<c>%USERPROFILE%\.videocortex\appsettings.Local.json</c>). Keys use the .NET configuration
/// colon syntax (<c>"Llm:BaseUrl"</c>) and are translated to nested <see cref="JsonObject"/>
/// traversal. Writes merge into the existing file (unrelated keys survive) and are atomic.
/// </summary>
public interface IOverlayWriter
{
    /// <summary>Loads the overlay as a mutable object (empty when the file is missing/blank).</summary>
    Task<JsonObject> LoadAsync(CancellationToken ct = default);

    /// <summary>Reads the string value at <paramref name="colonPath"/>, or null when absent.</summary>
    Task<string?> GetAsync(string colonPath, CancellationToken ct = default);

    /// <summary>
    /// Sets the value at <paramref name="colonPath"/>. A null/empty value <b>removes</b> the key
    /// (so the config chain falls through to <c>appsettings.json</c> instead of shadowing it with
    /// an empty string). Serializes the full load-modify-write cycle.
    /// </summary>
    Task SetAsync(string colonPath, string? value, CancellationToken ct = default);
}
