using System.Text.Json;
using System.Text.Json.Nodes;

namespace VideoCortex.Core.Services.Config;

/// <summary>
/// File-backed <see cref="IOverlayWriter"/>. Merges into the existing overlay (load-modify-write)
/// so keys other features wrote survive; writes atomically via a sibling <c>.tmp</c> file plus
/// <see cref="File.Move(string, string, bool)"/> with overwrite (JsonSerializer emits UTF-8 without
/// a BOM). A single <see cref="SemaphoreSlim"/> serializes concurrent writes.
/// </summary>
public sealed class OverlayWriter : IOverlayWriter
{
    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    private readonly string _path;
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>Defaults to <see cref="VideoCortexPaths.OverlayPath"/>.</summary>
    public OverlayWriter() : this(VideoCortexPaths.OverlayPath) { }

    public OverlayWriter(string overlayPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(overlayPath);
        _path = overlayPath;
    }

    public async Task<JsonObject> LoadAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try { return await LoadCoreAsync(ct); }
        finally { _gate.Release(); }
    }

    public async Task<string?> GetAsync(string colonPath, CancellationToken ct = default)
    {
        var segments = SplitPath(colonPath);
        await _gate.WaitAsync(ct);
        try
        {
            JsonNode? cursor = await LoadCoreAsync(ct);
            foreach (var segment in segments)
            {
                if (cursor is not JsonObject obj || !obj.TryGetPropertyValue(segment, out var next))
                    return null;
                cursor = next;
            }
            return cursor switch
            {
                null => null,
                JsonValue v when v.TryGetValue<string>(out var s) => s,
                _ => cursor.ToString(),
            };
        }
        finally { _gate.Release(); }
    }

    public async Task SetAsync(string colonPath, string? value, CancellationToken ct = default)
    {
        var segments = SplitPath(colonPath);
        await _gate.WaitAsync(ct);
        try
        {
            var root = await LoadCoreAsync(ct);
            if (string.IsNullOrEmpty(value)) RemoveAt(root, segments);
            else SetAt(root, segments, value);
            await SaveCoreAsync(root, ct);
        }
        finally { _gate.Release(); }
    }

    private async Task<JsonObject> LoadCoreAsync(CancellationToken ct)
    {
        if (!File.Exists(_path)) return new JsonObject();
        await using var fs = File.OpenRead(_path);
        if (fs.Length == 0) return new JsonObject();
        var node = await JsonNode.ParseAsync(fs, cancellationToken: ct);
        return node as JsonObject ?? new JsonObject();
    }

    private async Task SaveCoreAsync(JsonObject root, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var tmp = _path + ".tmp";
        await using (var fs = File.Create(tmp))
        {
            await JsonSerializer.SerializeAsync(fs, root, WriteOptions, ct);
        }
        File.Move(tmp, _path, overwrite: true);
    }

    private static string[] SplitPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var segments = path.Split(':', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
            throw new ArgumentException($"Path must contain at least one segment: '{path}'.", nameof(path));
        return segments;
    }

    private static void SetAt(JsonObject root, string[] segments, string value)
    {
        var cursor = root;
        for (var i = 0; i < segments.Length - 1; i++)
        {
            if (cursor[segments[i]] is JsonObject child)
            {
                cursor = child;
            }
            else
            {
                var fresh = new JsonObject();
                cursor[segments[i]] = fresh;
                cursor = fresh;
            }
        }
        cursor[segments[^1]] = JsonValue.Create(value);
    }

    private static void RemoveAt(JsonObject root, string[] segments)
    {
        var cursor = root;
        for (var i = 0; i < segments.Length - 1; i++)
        {
            if (cursor[segments[i]] is not JsonObject child) return;
            cursor = child;
        }
        cursor.Remove(segments[^1]);
    }
}
