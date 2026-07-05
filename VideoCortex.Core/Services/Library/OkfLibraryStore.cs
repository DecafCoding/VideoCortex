using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using VideoCortex.Core.Entities;

namespace VideoCortex.Core.Services.Library;

/// <summary>
/// File-backed <see cref="IOkfLibraryStore"/>. Writes each project's OKF-HTML library into
/// <c>&lt;rootPath&gt;/&lt;Project.Name&gt;/</c>, reading the shared <c>theme.css</c> and root-index
/// template from <paramref name="templatesDir"/> (the bundled <c>wwwroot/okf</c>). All writes
/// are atomic (temp + move-overwrite) and UTF-8 without BOM.
/// </summary>
public sealed partial class OkfLibraryStore : IOkfLibraryStore
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly string _rootPath;
    private readonly string _templatesDir;

    public OkfLibraryStore(string rootPath, string templatesDir)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(templatesDir);
        _rootPath = rootPath;
        _templatesDir = templatesDir;
    }

    public async Task CreateLibraryAsync(Project project, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(project);

        var folderName = SanitizeFolderName(project.Name);
        var dir = Path.Combine(_rootPath, folderName);
        Directory.CreateDirectory(dir); // creates _rootPath too if absent

        // 1) Copy theme.css byte-for-byte so generated libraries match hand-built ones exactly.
        var themeBytes = await File.ReadAllBytesAsync(Path.Combine(_templatesDir, "theme.css"), ct);
        await AtomicWriteBytesAsync(Path.Combine(dir, "theme.css"), themeBytes, ct);

        // 2) Write an OKF-conformant empty root index.html from the bundled template.
        var template = await File.ReadAllTextAsync(Path.Combine(_templatesDir, "index.html"), Utf8NoBom, ct);
        var html = RenderEmptyIndex(template, project);
        await AtomicWriteTextAsync(Path.Combine(dir, "index.html"), html, ct);
    }

    /// <summary>
    /// Fills the root-index template for a library with no concepts yet: title/description/
    /// theme href filled, the sample <c>okf-index-group</c> block removed. The okf-meta title
    /// is JSON-encoded; the visible title/description are HTML-encoded.
    /// </summary>
    private static string RenderEmptyIndex(string template, Project project)
    {
        var name = project.Name.Trim();

        // The okf-meta JSON occurrence is the only one wrapped in quotes: "{{LIBRARY_TITLE}}".
        // Replace it first with a JSON-encoded string literal so the metadata always parses.
        var html = template.Replace("\"{{LIBRARY_TITLE}}\"", JsonSerializer.Serialize(name));

        // Remaining occurrences are HTML text (<title>, <h1>).
        html = html.Replace("{{LIBRARY_TITLE}}", WebUtility.HtmlEncode(name));
        html = html.Replace("{{THEME_HREF}}", "theme.css"); // relative — never "/theme.css" (§3.2)
        html = html.Replace("{{LIBRARY_DESCRIPTION}}", WebUtility.HtmlEncode(project.Description ?? string.Empty));

        // Remove the sample index-group block (no concepts yet). Phase 6 fills the report.
        html = IndexGroupBlock().Replace(html, string.Empty);
        return html;
    }

    /// <summary>
    /// Sanitizes a project display name into a safe on-disk folder name. Spaces are preserved
    /// (folders are the human name — e.g. <c>Wild Flowers</c>); path-traversal is rejected.
    /// Exposed so callers can compute the same folder name the store writes to.
    /// </summary>
    public static string SanitizeFolderName(string? name)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            throw new ArgumentException("Project name is required.", nameof(name));
        if (trimmed is "." or "..")
            throw new ArgumentException($"Invalid project folder name '{trimmed}'.", nameof(name));
        if (trimmed.Contains("..") || trimmed.Contains('/') || trimmed.Contains('\\'))
            throw new ArgumentException($"Unsafe project folder name '{trimmed}'.", nameof(name));
        return trimmed;
    }

    private static async Task AtomicWriteTextAsync(string finalPath, string content, CancellationToken ct)
    {
        var tempPath = finalPath + ".tmp";
        await File.WriteAllTextAsync(tempPath, content, Utf8NoBom, ct);
        File.Move(tempPath, finalPath, overwrite: true);
    }

    private static async Task AtomicWriteBytesAsync(string finalPath, byte[] content, CancellationToken ct)
    {
        var tempPath = finalPath + ".tmp";
        await File.WriteAllBytesAsync(tempPath, content, ct);
        File.Move(tempPath, finalPath, overwrite: true);
    }

    [GeneratedRegex("""\s*<section class="okf-index-group">.*?</section>""", RegexOptions.Singleline)]
    private static partial Regex IndexGroupBlock();
}
