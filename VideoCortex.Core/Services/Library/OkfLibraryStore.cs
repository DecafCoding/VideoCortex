using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Markdig;
using VideoCortex.Core.Entities;
using VideoCortex.Core.Services.Llm;
using VideoCortex.Core.Services.Utilities;

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
    private static readonly MarkdownPipeline MarkdigPipeline =
        new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

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

    public async Task<string> WriteConceptPageAsync(
        Project project, Video video, VideoSummary summary, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(video);
        ArgumentNullException.ThrowIfNull(summary);

        var dir = Path.Combine(_rootPath, SanitizeFolderName(project.Name));
        Directory.CreateDirectory(dir);

        // Reuse an existing slug (re-summarize overwrites the same file); otherwise derive a
        // project-unique one from the summary title, falling back to the video id.
        var slug = string.IsNullOrWhiteSpace(video.ConceptSlug)
            ? ResolveConceptSlug(dir, summary.Title, video.YoutubeVideoId)
            : video.ConceptSlug;

        var bodyHtml = Markdown.ToHtml(summary.BodyMarkdown, MarkdigPipeline);
        var template = await File.ReadAllTextAsync(Path.Combine(_templatesDir, "concept.html"), Utf8NoBom, ct);
        var html = RenderConcept(template, summary, video, bodyHtml);
        await AtomicWriteTextAsync(Path.Combine(dir, slug + ".html"), html, ct);
        return slug;
    }

    public async Task WriteReportAsync(
        Project project, string libraryDescription,
        IReadOnlyList<ReportItem> items, IReadOnlyList<ReportSource> sources, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(items);

        var dir = Path.Combine(_rootPath, SanitizeFolderName(project.Name));
        Directory.CreateDirectory(dir);

        var name = project.Name.Trim();
        var template = await File.ReadAllTextAsync(Path.Combine(_templatesDir, "index.html"), Utf8NoBom, ct);

        // okf-meta title is JSON-encoded (quoted in the template); visible title/description are HTML.
        var html = template.Replace("\"{{LIBRARY_TITLE}}\"", JsonSerializer.Serialize(name));
        html = html.Replace("{{LIBRARY_TITLE}}", WebUtility.HtmlEncode(name));
        html = html.Replace("{{THEME_HREF}}", "theme.css");
        html = html.Replace("{{LIBRARY_DESCRIPTION}}", WebUtility.HtmlEncode(libraryDescription ?? string.Empty));

        // Replace the sample index-group region with the rendered running list. MatchEvaluator
        // avoids Regex '$' substitution in the body.
        var body = RenderReportBody(items, sources ?? Array.Empty<ReportSource>());
        html = IndexGroupBlock().Replace(html, _ => "\n    " + body);

        await AtomicWriteTextAsync(Path.Combine(dir, "index.html"), html, ct);
    }

    /// <summary>
    /// Renders the aggregated items into the report's inner body: one <c>&lt;h2&gt;</c> per item, its
    /// Markdig-rendered body, then a "Sources" line of relative concept links. Item titles and source
    /// link text are HTML-encoded; the Markdown body is rendered like a concept page. A source slug is
    /// only linked when it resolves to a known concept (via <paramref name="sources"/>), so a stray or
    /// hallucinated slug can never produce a broken or foreign link.
    /// </summary>
    private static string RenderReportBody(IReadOnlyList<ReportItem> items, IReadOnlyList<ReportSource> sources)
    {
        var titleBySlug = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var s in sources)
            if (!string.IsNullOrWhiteSpace(s.Slug))
                titleBySlug[s.Slug] = s.Title;

        var sb = new StringBuilder();
        foreach (var item in items)
        {
            sb.Append("<h2>").Append(WebUtility.HtmlEncode(item.Title.Trim())).Append("</h2>\n");
            sb.Append(Markdown.ToHtml(item.BodyMarkdown ?? string.Empty, MarkdigPipeline));

            var links = (item.SourceSlugs ?? Array.Empty<string>())
                .Select(NormalizeSlug)
                .Where(titleBySlug.ContainsKey)
                .Distinct(StringComparer.Ordinal)
                .Select(slug => $"<a href=\"{slug}.html\">{WebUtility.HtmlEncode(titleBySlug[slug])}</a>")
                .ToList();
            if (links.Count > 0)
                sb.Append("<p class=\"okf-sources\"><strong>Sources:</strong> ")
                  .Append(string.Join(", ", links))
                  .Append("</p>\n");
        }
        return sb.ToString();
    }

    /// <summary>Tolerates a model that appends ".html" to a source slug; strips it so it can match.</summary>
    private static string NormalizeSlug(string slug)
    {
        slug = slug.Trim();
        return slug.EndsWith(".html", StringComparison.OrdinalIgnoreCase) ? slug[..^5] : slug;
    }

    public Task DeleteConceptPageAsync(Project project, Video video, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(video);

        var slug = video.ConceptSlug;
        if (string.IsNullOrWhiteSpace(slug)) return Task.CompletedTask;
        if (!SafeFileNameRegex().IsMatch(slug))
            throw new ArgumentException($"Unsafe concept slug '{slug}'.", nameof(video));

        var path = Path.Combine(_rootPath, SanitizeFolderName(project.Name), slug + ".html");
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    private static string ResolveConceptSlug(string dir, string title, string fallbackId)
    {
        var baseSlug = SlugHelper.ToSlug(title, SlugHelper.ToSlug(fallbackId, "video"));
        var candidate = baseSlug;
        var n = 1;
        while (File.Exists(Path.Combine(dir, candidate + ".html")))
        {
            n++;
            candidate = $"{baseSlug}-{n}";
            if (n > 1000) { candidate = $"{baseSlug}-{Guid.NewGuid():N}"; break; }
        }
        return candidate;
    }

    /// <summary>
    /// Fills the concept template. okf-meta string values are JSON-encoded (they sit inside
    /// quotes in the template); the visible title/type/tag text is HTML-encoded; <c>{{TAGS}}</c>
    /// is the array *contents* and <c>{{RESOURCE}}</c> a bare JSON value — neither double-wrapped.
    /// The Markdig HTML goes into <c>{{BODY}}</c> last so it is never re-scanned for placeholders.
    /// </summary>
    private static string RenderConcept(string template, VideoSummary summary, Video video, string bodyHtml)
    {
        var title = (summary.Title ?? string.Empty).Trim();
        var tags = summary.Tags ?? Array.Empty<string>();
        var resource = string.IsNullOrWhiteSpace(video.YoutubeVideoId)
            ? "null"
            : JsonSerializer.Serialize($"https://www.youtube.com/watch?v={video.YoutubeVideoId}");

        // Quoted okf-meta occurrences first (JSON-encoded), then bare HTML occurrences.
        var html = template
            .Replace("\"{{TITLE}}\"", JsonSerializer.Serialize(title))
            .Replace("\"{{TYPE}}\"", JsonSerializer.Serialize("Video"))
            .Replace("\"{{DESCRIPTION}}\"", JsonSerializer.Serialize(summary.Description ?? string.Empty));

        html = html
            .Replace("{{THEME_HREF}}", "theme.css")
            .Replace("{{TITLE}}", WebUtility.HtmlEncode(title))
            .Replace("{{TYPE}}", "Video")
            .Replace("{{TAGS}}", string.Join(", ", tags.Select(t => JsonSerializer.Serialize(t))))
            .Replace("{{RESOURCE}}", resource)
            .Replace("{{TIMESTAMP}}", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"))
            .Replace("{{TAG_CHIPS}}", string.Join("\n      ",
                tags.Select(t => $"<span class=\"okf-tag\">{WebUtility.HtmlEncode(t)}</span>")))
            .Replace("{{BODY}}", bodyHtml);

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

    [GeneratedRegex("^[A-Za-z0-9._-]+$")]
    private static partial Regex SafeFileNameRegex();
}
